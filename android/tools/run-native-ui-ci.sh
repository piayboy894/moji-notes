#!/usr/bin/env bash
set -uo pipefail

moji_emulator=$(adb shell getprop ro.kernel.qemu | tr -d '\r')
if [ "$moji_emulator" != "1" ]; then
  echo 'Native CI checks require a disposable Android emulator.'
  exit 1
fi

# The CI image's Pixel Launcher can ANR while Google initializes background
# models, covering the application with an unrelated modal dialog. The launcher
# is not part of these tests. Disable only that package on the disposable emulator;
# errors from Moji remain visible and still fail the tests.
adb shell pm disable-user --user 0 com.google.android.apps.nexuslauncher || true

# Retain captures even if a native assertion fails before the emulator shuts down.
./gradlew :app:connectedDebugAndroidTest --no-daemon
moji_test_status=$?
adb pull /sdcard/Download/moji-native-ui ../android-ui-screenshots
moji_capture_status=$?

if [ "$moji_test_status" -ne 0 ]; then
  exit "$moji_test_status"
fi
if [ "$moji_capture_status" -ne 0 ]; then
  exit "$moji_capture_status"
fi
set -e
if [ -n "${MOJI_KEYSTORE_PATH:-}" ]; then
  python3 tools/release-install-smoke.py
  python3 tools/release-editor-smoke.py
fi
