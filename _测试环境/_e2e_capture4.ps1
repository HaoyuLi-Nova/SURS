$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$sim = "D:\Projects\SURS\_测试环境\分发模拟-医院TEST"
$exe = Join-Path $sim "SURS.App.exe"
$pngMain = Join-Path $sim "截图-报告编辑.png"
$pngLogin = Join-Path $sim "截图-登录窗口.png"
foreach($f in @($pngMain,$pngLogin)){ if(Test-Path $f){ Remove-Item $f -Force } }

Add-Type -TypeDefinition @"
using System;
using System.Threading;
using System.Runtime.InteropServices;
public static class SursInput {
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern ushort MapVirtualKey(uint uCode, uint uMapType);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION U; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    const uint UF = 0x0004;
    const uint UP = 0x0002;
    static INPUT UniChar(char c, uint add) {
        INPUT i = new INPUT(); i.type = 1; i.U.ki.wVk = 0; i.U.ki.wScan = c; i.U.ki.dwFlags = UF | add;
        return i;
    }
    public static void TypeText(string s) {
        int n = s.Length * 2; INPUT[] a = new INPUT[n]; int k = 0;
        foreach(char c in s) { a[k++] = UniChar(c, 0); a[k++] = UniChar(c, UP); }
        SendInput((uint)n, a, Marshal.SizeOf(typeof(INPUT)));
    }
    static INPUT KeyVk(ushort vk, uint flags) {
        INPUT i = new INPUT(); i.type = 1; i.U.ki.wVk = vk; i.U.ki.wScan = MapVirtualKey(vk, 0); i.U.ki.dwFlags = flags;
        return i;
    }
    public static void Paste() {
        INPUT[] a = new INPUT[]{ KeyVk(0x11,0), KeyVk(0x56,0), KeyVk(0x56,UP), KeyVk(0x11,UP) };
        SendInput(4, a, Marshal.SizeOf(typeof(INPUT)));
    }
    public static void Enter() {
        INPUT[] a = new INPUT[]{ KeyVk(0x0D,0), KeyVk(0x0D,UP) };
        SendInput(2, a, Marshal.SizeOf(typeof(INPUT)));
    }
    public static void ForceForeground(IntPtr h) {
        for(int i=0;i<8;i++){
            keybd_event(0x12, 0x38, 0, UIntPtr.Zero);
            keybd_event(0x12, 0x38, 0x0002, UIntPtr.Zero);
            Thread.Sleep(60);
            if(SetForegroundWindow(h) && GetForegroundWindow() == h) return;
            Thread.Sleep(120);
        }
    }
}
"@
[SursInput]::SetProcessDPIAware() | Out-Null

function Find-SursWindow($titlesRe, $timeoutSec){
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  while((Get-Date) -lt $deadline){
    $procs = @(Get-Process SURS.App -ErrorAction SilentlyContinue)
    foreach($pp in $procs){ try { $pp.Refresh() } catch {}
      if($pp.MainWindowHandle -ne [IntPtr]::Zero){
        if($pp.MainWindowTitle -match $titlesRe){ return $pp }
      }
    }
    Start-Sleep -Milliseconds 600
  }
  return $null
}
function Capture-Window($hwnd, $path){
  $rect = New-Object SursInput+RECT
  [SursInput]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
  $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
  if($w -le 0 -or $h -le 0){ Write-Host ("  跳过异常尺寸: " + $w + "x" + $h); return }
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Host ("  截图: " + $path + "  " + $w + "x" + $h)
}
function Dump-Texts($aeRoot){
  $condText = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $texts = $aeRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condText)
  for($i=0;$i -lt $texts.Count;$i++){
    $n = $texts.Item($i).Current.Name
    if($n){ Write-Host ("   [文本] " + $n) }
  }
}

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $exe -WorkingDirectory $sim -PassThru
Write-Host "已启动，等待登录窗口(90s)..."
$proc = Find-SursWindow "首次激活|登录|续期" 90
if(-not $proc){ Write-Host "FAIL: 未找到登录窗口"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }
Write-Host ("找到窗口: [" + $proc.MainWindowTitle + "] PID=" + $proc.Id)

[SursInput]::ForceForeground($proc.MainWindowHandle)
Start-Sleep -Seconds 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

$condEdit = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condEdit)
$pw = $null
for($i=0;$i -lt $edits.Count;$i++){
  $e = $edits.Item($i)
  if($e.Current.IsPassword){ $pw = $e; break }
}
if(-not $pw){ Write-Host "FAIL: 未找到密码框"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }
$pw.SetFocus()
Start-Sleep -Milliseconds 500

# 剪贴板粘贴（绕开中文输入法）
$clipOk = $false
for($try=0;$try -lt 12 -and -not $clipOk;$try++){
  try { [System.Windows.Clipboard]::SetText("TestAbc123!"); $clipOk = $true }
  catch { Start-Sleep -Milliseconds 250 }
}
if(-not $clipOk){ Write-Host "FAIL: 剪贴板无法打开"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }
Start-Sleep -Milliseconds 200
[SursInput]::Paste()
Start-Sleep -Milliseconds 500
Write-Host "已通过剪贴板粘贴密码"

$condBtn = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condBtn)
$target2 = $null
for($i=0;$i -lt $btns.Count;$i++){
  $b = $btns.Item($i); $nm = $b.Current.Name
  if($nm -match "登录"){ $target2 = $b; break }
}
if($target2){ $target2.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host ("已点按钮: " + $target2.Current.Name) }
if(-not $target2){ Write-Host "警告: 未找到登录按钮，改为回车"; [SursInput]::Enter() }

Write-Host "等待主窗口(60s)..."
$main = Find-SursWindow "妇科超声结构化报告系统" 60
if($main){
  Write-Host ("进入主窗口: " + $main.MainWindowTitle)
  Start-Sleep -Seconds 3
  Capture-Window $main.MainWindowHandle $pngMain
  Write-Host ("主窗口截图: " + $pngMain)
}else{
  Write-Host "FAIL: 未进入主窗口（登录被拒）。以下为登录窗口当前文本："
  $proc2 = Find-SursWindow "首次激活|登录|续期" 10
  if($proc2){
    $root2 = [System.Windows.Automation.AutomationElement]::FromHandle($proc2.MainWindowHandle)
    Dump-Texts $root2
    Capture-Window $proc2.MainWindowHandle $pngLogin
  }
}

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "DONE"
