# SURS 测试辅助：把本机授权状态重置为未激活，回到首次激活页。
# 仅限测试机；正式医院电脑请勿运行。
$ErrorActionPreference = "Stop"

# 1) 关闭正在运行的 SURS
Get-Process SURS.App -ErrorAction SilentlyContinue | Stop-Process -Force

# 2) 清空三副本：主档 / 影子 / 注册表镜像
$targets = @(
  (Join-Path $env:LOCALAPPDATA "SURS\License"),
  (Join-Path $env:ProgramData "SURS\License")
)
foreach ($d in $targets) {
  if (Test-Path -LiteralPath $d) {
    Remove-Item -LiteralPath $d -Recurse -Force
    Write-Host "已清除授权目录: $d"
  }
}
if (Test-Path "HKCU:\Software\SURS\License") {
  Remove-Item -LiteralPath "HKCU:\Software\SURS\License" -Recurse -Force
  Write-Host "已清除注册表镜像: HKCU\Software\SURS\License"
}

Write-Host ""
Write-Host "完成。现在双击 分发模拟-医院TEST 目录里的 SURS.App.exe 即可回到首次激活页。"