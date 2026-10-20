# 创建一份本机自签名代码签名证书，并把它装进当前用户的受信任根存储。
# 这一步只需要做一次；之后 deploy.ps1 会自动用它给构建产物签名。
#
# 说明：这是给本机自用准备的证书，不是受全局信任的正式证书。别人下载你的
# 发布包时仍然会看到「发布者未知」，要消除那一步只能购买 OV/EV 代码签名证书。
$ErrorActionPreference = 'Stop'

$subject = 'CN=PCL-HE Project'
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject } |
    Select-Object -First 1

if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'PCL-HE Code Signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "已创建证书 $($cert.Thumbprint)"
} else {
    Write-Host "复用已有证书 $($cert.Thumbprint)"
}

$cerPath = Join-Path (Join-Path $env:USERPROFILE 'PCL-HE') 'pcl-he-codesign.cer'
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null

foreach ($store in @('Root', 'TrustedPublisher')) {
    $target = "Cert:\CurrentUser\$store\$($cert.Thumbprint)"
    if (-not (Test-Path $target)) {
        Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\$store" | Out-Null
        Write-Host "已导入 $store"
    } else {
        Write-Host "$store 中已存在"
    }
}

Write-Host '完成。现在运行 deploy.ps1 即可签名并部署。'
