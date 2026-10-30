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

# 找窗口：遍历所有 SURS.App 进程，取有可见窗口且有标题的那个
function Find-SursWindow($titlesRe, $timeoutSec){
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  while((Get-Date) -lt $deadline){
    $procs = @(Get-Process SURS.App -ErrorAction SilentlyContinue)
    foreach($pp in $procs){
      try { $pp.Refresh() } catch {}
      if($pp.MainWindowHandle -ne [IntPtr]::Zero){
        $t = $pp.MainWindowTitle
        if($t -match $titlesRe){ return ,@($pp, $t) }
      }
    }
    Start-Sleep -Milliseconds 600
  }
  return $null
}

# 1) 启动并等待登录/激活窗口
$p = Start-Process -FilePath $exe -WorkingDirectory $sim -PassThru
Write-Host "已启动，等待登录/激活窗口(90s)..."
$found = Find-SursWindow "首次激活|登录|续期" 90
if(-not $found){
  Write-Host "FAIL: 未找到登录窗口。当前进程与标题如下："
  Get-Process SURS.App -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ("  PID=" + $_.Id + " 标题=[" + $_.MainWindowTitle + "] hwnd=" + $_.MainWindowHandle) }
  Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  exit 1
}
$proc = $found[0]; $title = $found[1]
Write-Host ("找到窗口: [" + $title + "] PID=" + $proc.Id)

# 2) 激活并发送密码
[Microsoft.VisualBasic.Interaction]::AppActivate($proc.Id) | Out-Null
Start-Sleep -Milliseconds 600
[SursWin32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Seconds 1

if($title -match "首次激活"){
  # 激活页：卡号框聚焦 → TAB TAB 到密码
  [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
  [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
  Start-Sleep -Milliseconds 300
}
[System.Windows.Forms.SendKeys]::SendWait("TestAbc123!")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Write-Host "已发送密码+回车"

# 3) 等待主窗口
Write-Host "等待主窗口(60s)..."
$main = Find-SursWindow "妇科超声结构化报告系统" 60
if(-not $main){
  Write-Host "FAIL: 未进入主窗口。"
  Get-Process SURS.App -ErrorAction SilentlyContinue | ForEach-Object { try{$_.Refresh()}catch{}; Write-Host ("  PID=" + $_.Id + " 标题=[" + $_.MainWindowTitle + "]") }
  Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  exit 1
}
$mh = $main[0].MainWindowHandle

Start-Sleep -Seconds 3

# 4) 截取主窗口
$rect = New-Object SursWin32+RECT
[SursWin32]::GetWindowRect($mh, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host ("主窗口尺寸: {0}x{1}" -f $w,$h)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host ("截图已保存: " + $png)

Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "DONE"
