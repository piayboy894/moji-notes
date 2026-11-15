#!/usr/bin/env bash
set -uo pipefail
[[ $(adb shell getprop ro.kernel.qemu | tr -d '\r') == 1 ]] || exit 1
[[ -n "${MOJI_KEYSTORE_PATH:-}" ]] || exit 1
adb shell pm disable-user --user 0 com.google.android.apps.nexuslauncher || true
python3 tools/release-install-smoke.py
moji_install_status=$?
python3 tools/release-editor-smoke.py
moji_editor_status=$?
if [ "$moji_install_status" -ne 0 ] || [ "$moji_editor_status" -ne 0 ]; then exit 1; fi
