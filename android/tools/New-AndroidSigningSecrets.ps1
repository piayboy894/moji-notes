& {
# Run in Windows PowerShell as your normal user. No administrator rights or JDK required.
$ErrorActionPreference = 'Stop'
$signingDirectory = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Moji-Android-Signing'
$keystoreFile = Join-Path $signingDirectory 'moji-android-release.p12'
$protectedPasswordFile = Join-Path $signingDirectory 'password.dpapi'
New-Item -ItemType Directory -Force $signingDirectory | Out-Null

if (Test-Path $keystoreFile) {
    if (-not (Test-Path $protectedPasswordFile)) { throw 'Existing key found. Restore its saved password rather than generating a different signing identity.' }
    $securePassword = Get-Content $protectedPasswordFile -Raw | ConvertTo-SecureString
    $signingPassword = [System.Net.NetworkCredential]::new('', $securePassword).Password
} else {
    $randomBytes = New-Object byte[] 36
    $randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $randomGenerator.GetBytes($randomBytes)
    $randomGenerator.Dispose()
    $signingPassword = [Convert]::ToBase64String($randomBytes)
    $securePassword = ConvertTo-SecureString $signingPassword -AsPlainText -Force
    $certificate = New-SelfSignedCertificate -Type Custom -Subject 'CN=Moji Notes Android' -FriendlyName 'moji-android' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy Exportable -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(27)
    Export-PfxCertificate -Cert $certificate -FilePath $keystoreFile -Password $securePassword | Out-Null
    $securePassword | ConvertFrom-SecureString | Set-Content $protectedPasswordFile
}

$keyBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($keystoreFile))
Set-Clipboard -Value $keyBase64
Write-Host ''
Write-Host '1. The keystore value is now on your clipboard.' -ForegroundColor Green
Write-Host '   GitHub: Settings > Secrets and variables > Actions > New repository secret'
Write-Host '   Name: ANDROID_KEYSTORE_BASE64'
Write-Host '   Secret: paste the clipboard value.'
Read-Host 'After saving the first Secret, press Enter'

Set-Clipboard -Value $signingPassword
Write-Host ''
Write-Host '2. The keystore password is now on your clipboard.' -ForegroundColor Green
Write-Host '   Name: ANDROID_KEYSTORE_PASSWORD'
Write-Host '   Secret: paste the clipboard value.'
Write-Host '   Also save this password in your password manager.'
Read-Host 'After saving the second Secret and backing up its password, press Enter'

Set-Clipboard -Value ' '
$keyBase64 = $null
$signingPassword = $null
Write-Host ''
Write-Host ('Keep this private signing backup: ' + $keystoreFile) -ForegroundColor Yellow
Write-Host 'password.dpapi can recover the password only under this Windows user profile.'
Write-Host 'Do not upload these files to the source repository or send the values in chat.'
Write-Host 'The same signing key must be retained for future APK updates.'

}
