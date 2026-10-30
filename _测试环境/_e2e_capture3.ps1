$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$sim = "D:\Projects\SURS\_测试环境\分发模拟-医院TEST"
$exe = Join-Path $sim "SURS.App.exe"
$png = Join-Path $sim "截图-报告编辑.png"
if(Test-Path $png){ Remove-Item $png -Force }

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class SursInput {
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
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
    public static void Enter() {
        INPUT down = new INPUT(); down.type = 1; down.U.ki.wVk = 0x0D; down.U.ki.wScan = 0x1C;
        INPUT up = down; up.U.ki.dwFlags = UP;
        SendInput(2, new INPUT[]{ down, up }, Marshal.SizeOf(typeof(INPUT)));
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

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $exe -WorkingDirectory $sim -PassThru
Write-Host "已启动，等待登录/激活窗口(90s)..."
$proc = Find-SursWindow "首次激活|登录|续期" 90
if(-not $proc){ Write-Host "FAIL: 未找到登录窗口"; Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 1 }
Write-Host ("找到窗口: [" + $proc.MainWindowTitle + "] PID=" + $proc.Id)

[Microsoft.VisualBasic.Interaction]::AppActivate($proc.Id) | Out-Null
Start-Sleep -Milliseconds 600
[SursInput]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
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
Start-Sleep -Milliseconds 400

[SursInput]::TypeText("TestAbc123!")
Start-Sleep -Milliseconds 400
Write-Host "密码已输入"

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
if(-not $main){
  Write-Host "FAIL: 未进入主窗口"
  Get-Process SURS.App -ErrorAction SilentlyContinue | ForEach-Object { try{$_.Refresh()}catch{}; Write-Host ("  PID=" + $_.Id + " 标题=[" + $_.MainWindowTitle + "]") }
  Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force
  exit 1
}
$mh = $main.MainWindowHandle
Start-Sleep -Seconds 3

$rect = New-Object SursInput+RECT
[SursInput]::GetWindowRect($mh, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
Write-Host ("主窗口: {0}x{1}" -f $w,$h)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host ("截图已保存: " + $png)

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "DONE"
