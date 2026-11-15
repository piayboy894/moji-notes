$ErrorActionPreference = 'Stop'
$folder = Join-Path $pwd 'webview-bootstrapper'
New-Item -ItemType Directory -Force $folder | Out-Null
$setup = Join-Path $folder 'MicrosoftEdgeWebview2Setup.exe'
Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $setup
$signature = Get-AuthenticodeSignature $setup
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
  throw 'WebView2 bootstrapper is not authentically signed by Microsoft.'
}
Get-FileHash $setup -Algorithm SHA256 | Select-Object Algorithm,Hash
