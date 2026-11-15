$ErrorActionPreference = 'Stop'
$manifest = Get-Content assets/webview-runtime.json | ConvertFrom-Json
New-Item -ItemType Directory -Force webview-runtime | Out-Null
foreach ($arch in @('x64','arm64')) {
  $cab = Join-Path $pwd "webview-runtime/$arch.cab"
  if (!(Test-Path $cab)) { Invoke-WebRequest $manifest.urls.$arch -OutFile $cab }
  $check = Join-Path $env:RUNNER_TEMP "moji-runtime-$arch"
  New-Item -ItemType Directory -Force $check | Out-Null
  & expand.exe $cab '-F:*' $check | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'WebView runtime archive is invalid' }
  $engine = Get-ChildItem $check -Filter msedgewebview2.exe -Recurse | Select-Object -First 1
  $signature = Get-AuthenticodeSignature $engine.FullName
  if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Runtime is not authentically signed by Microsoft' }
  Remove-Item $check -Recurse -Force
}
