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
using System.Runtime.InteropServices;
public static class SursNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    const uint WM_CHAR = 0x0102;
    public static void TypeText(IntPtr h, string s) {
        foreach(char c in s) { SendMessage(h, WM_CHAR, (IntPtr)(int)c, IntPtr.Zero); }
    }
    public static void ForceForeground(IntPtr h) {
        for(int i=0;i<6;i++){
            keybd_event(0x12, 0x38, 0, UIntPtr.Zero);
            keybd_event(0x12, 0x38, 0x0002, UIntPtr.Zero);
            System.Threading.Thread.Sleep(60);
            if(SetForegroundWindow(h)) return;
            System.Threading.Thread.Sleep(120);
        }
    }
}
"@
[SursNative]::SetProcessDPIAware() | Out-Null

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
  $rect = New-Object SursNative+RECT
  [SursNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
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

[SursNative]::ForceForeground($proc.MainWindowHandle)
Start-Sleep -Seconds 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

$condEdit = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condEdit)
$acct = $null; $pw = $null
for($i=0;$i -lt $edits.Count;$i++){
  $e = $edits.Item($i)
  if($e.Current.IsPassword){ $pw = $e } else { if(-not $acct){ $acct = $e } }
}
Write-Host ("找到编辑框: 账号=" + ($null -ne $acct) + " 密码=" + ($null -ne $pw))

# 账号：读值（诊断）+ 强制写 hosTEST
if($acct){
  try{
    $vp = $acct.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    Write-Host ("  账号当前值=[" + $vp.Current.Value + "]")
    $vp.SetValue("hosTEST")
    Write-Host ("  已写账号 hosTEST，回读=[" + $vp.Current.Value + "]")
  }catch{ Write-Host ("  账号 ValuePattern 失败: " + $_.Exception.Message) }
}

if(-not $pw){ Write-Host "FAIL: 未找到密码框"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }
$pw.SetFocus()
Start-Sleep -Milliseconds 500
[SursNative]::TypeText($proc.MainWindowHandle, "TestAbc123!")
Start-Sleep -Milliseconds 500
Write-Host "已通过 WM_CHAR 输入密码"

$condBtn = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condBtn)
$target2 = $null
for($i=0;$i -lt $btns.Count;$i++){
  $b = $btns.Item($i); $nm = $b.Current.Name
  if($nm -match "登录"){ $target2 = $b; break }
}
if($target2){ $target2.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host ("已点按钮: " + $target2.Current.Name) }
if(-not $target2){ Write-Host "FAIL: 未找到登录按钮"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }

Write-Host "等待主窗口(60s)..."
$main = Find-SursWindow "妇科超声结构化报告系统" 60
if($main){
  Write-Host ("进入主窗口: " + $main.MainWindowTitle)
  Start-Sleep -Seconds 3
  Capture-Window $main.MainWindowHandle $pngMain
  Write-Host ("主窗口截图: " + $pngMain)
}else{
  Write-Host "FAIL: 未进入主窗口（登录被拒）。登录窗口当前文本："
  $proc2 = Find-SursWindow "首次激活|登录|续期" 10
  if($proc2){
    $root2 = [System.Windows.Automation.AutomationElement]::FromHandle($proc2.MainWindowHandle)
    Dump-Texts $root2
    Capture-Window $proc2.MainWindowHandle $pngLogin
  }else{ Write-Host "  登录窗口已消失，但主窗口未出现（异常）。" }
}

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "DONE"
