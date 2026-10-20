# 把 Release 构建产物同步到运行目录，并刷新桌面快捷方式。
# 运行目录放在 workspace 之外，因为 DSH 的沙箱会给 workspace 内启动的进程
# 套上 AppContainer 限制，导致程序无法写入 AppData，启动即退出。
$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $MyInvocation.MyCommand.Path
$source  = Join-Path $repo 'PCL-Harness Edition\bin\Release\net10.0-windows'
$target  = Join-Path $env:USERPROFILE 'PCL-HE'
$exe     = Join-Path $target 'PCL-HE.exe'
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'PCL-HE.lnk'

if (-not (Test-Path (Join-Path $source 'PCL-HE.exe'))) {
    throw "没有找到构建产物，请先运行：dotnet build 'Plain Craft Launcher 2.slnx' -c Release"
}

Get-Process -Name 'PCL-HE' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 保留运行目录里用户自己的游戏数据，只覆盖程序本体
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

$sh = New-Object -ComObject WScript.Shell
$lnk = $sh.CreateShortcut($desktop)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $target
$lnk.IconLocation = "$exe,0"
$lnk.Save()

Write-Host "已同步到 $target"
Write-Host "桌面快捷方式已指向 $exe"
