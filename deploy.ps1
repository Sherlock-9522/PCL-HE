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

# 预置便携模式标记目录：启动器检测到它就优先把共享配置与本地缓存写进这里，
# 运行目录下只剩「启动器 + CONFIG + PCL + .minecraft」；删掉本目录即退回 AppData。
$portable = Join-Path $target 'CONFIG'
New-Item -ItemType Directory -Force -Path $portable | Out-Null
$portableReadme = Join-Path $portable 'README.txt'
if (-not (Test-Path $portableReadme)) {
    @(
        'PCL-HE 便携模式目录。'
        '启动器会把共享配置与本地缓存写在同级的 PCL 文件夹，本目录用于存放便携数据。'
        '删除本目录后，配置会退回 %APPDATA%\PCLHE。'
    ) | Set-Content -Path $portableReadme -Encoding UTF8
}

# 用本机的自签名证书给 exe 签名，避免每次启动都被 Defender 提示发布者未知。
# 证书在首次运行时创建并导入「受信任的根证书颁发机构」与「受信任的发布者」。
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq 'CN=PCL-HE Project' } |
    Select-Object -First 1
if ($null -eq $cert) {
    Write-Warning '没有找到 PCL-HE 自签名证书，跳过签名。运行 setup-signing.ps1 创建。'
} else {
    Set-AuthenticodeSignature -Certificate $cert -FilePath $exe -HashAlgorithm SHA256 | Out-Null
    Write-Host "已签名：$((Get-AuthenticodeSignature $exe).Status)"
}
Unblock-File -Path $exe -ErrorAction SilentlyContinue

$sh = New-Object -ComObject WScript.Shell
$lnk = $sh.CreateShortcut($desktop)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $target
$lnk.IconLocation = "$exe,0"
$lnk.Save()

Write-Host "已同步到 $target"
Write-Host "桌面快捷方式已指向 $exe"
