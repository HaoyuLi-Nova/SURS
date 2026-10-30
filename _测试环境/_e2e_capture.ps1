$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic

$sim = "D:\Projects\SURS\_测试环境\分发模拟-医院TEST"
$exe = Join-Path $sim "SURS.App.exe"
$png = Join-Path $sim "截图-报告编辑.png"
if(Test-Path $png){ Remove-Item $png -Force }

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class SursWin32 {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
[SursWin32]::SetProcessDPIAware() | Out-Null

$p = Start-Process -FilePath $exe -WorkingDirectory $sim -PassThru
Write-Host "等待首次激活窗口..."
$t0 = Get-Date
$wnd = [IntPtr]::Zero
while((Get-Date) -lt $t0.AddSeconds(60)){
  $p.Refresh()
  if($p.MainWindowHandle -ne [IntPtr]::Zero -and $p.MainWindowTitle -match "首次激活"){ $wnd = $p.MainWindowHandle; break }
  Start-Sleep -Milliseconds 500
}
if($wnd -eq [IntPtr]::Zero){ Write-Host "FAIL: 未出现首次激活窗口"; if(-not $p.HasExited){ Stop-Process -Id $p.Id -Force }; exit 1 }

# 让窗口获得激活 + 前台，再把焦点平稳交过去
[Microsoft.VisualBasic.Interaction]::AppActivate($p.Id) | Out-Null
Start-Sleep -Milliseconds 600
[SursWin32]::SetForegroundWindow($wnd) | Out-Null
Start-Sleep -Seconds 1

# 卡号框自动聚焦 → TAB 到账号 → TAB 到密码 → 输入 → 回车
[System.Windows.Forms.SendKeys]::SendWait("{TAB}")
[System.Windows.Forms.SendKeys]::SendWait("{TAB}")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("TestAbc123!")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

Write-Host "等待主窗口（报告编辑）..."
$mh = [IntPtr]::Zero
$t1 = Get-Date
while((Get-Date) -lt $t1.AddSeconds(60)){
  $p.Refresh()
  if($p.MainWindowTitle -match "妇科超声结构化报告系统"){ $mh = $p.MainWindowHandle; break }
  Start-Sleep -Milliseconds 500
}
if($mh -eq [IntPtr]::Zero){
  Write-Host "FAIL: 未进入主窗口（可能密码/聚焦有误）"
  if(-not $p.HasExited){ Stop-Process -Id $p.Id -Force }
  exit 1
}

Start-Sleep -Seconds 3

$rect = New-Object SursWin32+RECT
[SursWin32]::GetWindowRect($mh, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host ("窗口尺寸: {0}x{1}" -f $w,$h)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host ("截图已保存: " + $png)

if(-not $p.HasExited){ Stop-Process -Id $p.Id -Force }
Write-Host "DONE"
