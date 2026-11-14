#!/usr/bin/env bash
set -uo pipefail

# Retain captures even if a native assertion fails before the emulator shuts down.
./gradlew :app:connectedDebugAndroidTest --no-daemon
moji_test_status=$?
adb pull /sdcard/Download/moji-native-ui ../android-ui-screenshots
moji_capture_status=$?

if [ "$moji_test_status" -ne 0 ]; then
  exit "$moji_test_status"
fi
exit "$moji_capture_status"
