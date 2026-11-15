#!/usr/bin/env bash
set -euo pipefail
moji_apk=app/build/outputs/apk/release/app-release.apk
moji_signer="$ANDROID_HOME/build-tools/35.0.0/apksigner"
"$moji_signer" verify --verbose "$moji_apk"
moji_actual=$("$moji_signer" verify --print-certs "$moji_apk" | awk -F ': ' '/Signer #1 certificate SHA-256 digest:/ {print $2; exit}')
moji_expected=$(tr -d '[:space:]' < signing/publisher-certificate.sha256)
if [ "$moji_actual" != "$moji_expected" ]; then
  echo 'APK publisher certificate does not match the fixed project identity.'
  exit 1
fi
printf '%s\n' "$moji_actual" > app/build/outputs/apk/release/SIGNING-CERTIFICATE-SHA256.txt
echo 'Fixed publisher identity verified.'
