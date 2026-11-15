#!/usr/bin/env python3
"""Exercise the signed native release using Android's UI hierarchy and installer."""
import json
import re
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

PACKAGE = "app.moji.android"
PASSWORD = "Moji-Release-Smoke-2026"
MARKER = "MOJI_SIGNED_UPGRADE_PERSISTENCE_20261115"
APK = Path("app/build/outputs/apk/release/app-release.apk")
REPORT = Path("app/build/outputs/release-smoke/report.json")
checks = []

def adb(*args):
    result = subprocess.run(["adb", *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=70)
    if result.returncode:
        raise RuntimeError("Android command failed: " + " ".join(args[:3]))
    return result.stdout

def hierarchy():
    adb("shell", "uiautomator", "dump", "/sdcard/moji-release-window.xml")
    return ET.fromstring(adb("shell", "cat", "/sdcard/moji-release-window.xml"))

def wait_node(resource=None, text=None, contains=None, timeout=45):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        try:
            for node in hierarchy().iter("node"):
                identifier = node.attrib.get("resource-id", "")
                label = node.attrib.get("text", "")
                if resource and not (identifier == resource or identifier.endswith("/" + resource)):
                    continue
                if text is not None and label != text:
                    continue
                if contains is not None and contains not in label:
                    continue
                if not resource and text is None and contains is None:
                    continue
                return node
        except (ET.ParseError, RuntimeError):
            pass
        time.sleep(.6)
    raise RuntimeError("Native release UI did not reach: " + str(resource or text or contains))

def tap(node):
    bounds = [int(value) for value in re.findall(r"\d+", node.attrib["bounds"])]
    adb("shell", "input", "tap", str((bounds[0] + bounds[2]) // 2), str((bounds[1] + bounds[3]) // 2))
    time.sleep(.35)

def enter(resource, value):
    tap(wait_node(resource=resource))
    adb("shell", "input", "text", value)

def launch():
    output = adb("shell", "am", "start", "-W", "-n", PACKAGE + "/app.moji.android.MainActivity")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    (REPORT.parent / "launch.txt").write_text(output)

try:
    adb("shell", "settings", "put", "secure", "show_ime_with_hard_keyboard", "0")
    adb("install", "-r", str(APK)); launch()
    enter("auth-password", PASSWORD); enter("auth-confirm", PASSWORD)
    # The Enter key invokes the single-line field's Done action, closing the IME.
    adb("shell", "input", "keyevent", "KEYCODE_ENTER"); time.sleep(.5)
    tap(wait_node(resource="create-vault")); wait_node(resource="home-title")
    checks.append("signed production APK installed, launched and created an encrypted space")
    tap(wait_node(resource="book-Text")); tap(wait_node(contains="把生活，写成喜欢的样子"))
    tap(wait_node(resource="note-content")); adb("shell", "input", "keycombination", "KEYCODE_CTRL_LEFT", "KEYCODE_A")
    adb("shell", "input", "text", MARKER); time.sleep(1.5)
    adb("shell", "am", "force-stop", PACKAGE)
    adb("install", "-r", str(APK)); launch()
    enter("auth-password", PASSWORD); adb("shell", "input", "keyevent", "KEYCODE_ENTER"); time.sleep(.5)
    tap(wait_node(resource="create-vault")); wait_node(resource="home-title")
    tap(wait_node(resource="book-Text")); tap(wait_node(contains="把生活，写成喜欢的样子"))
    wait_node(resource="note-content", contains=MARKER)
    checks.append("cover installation preserved encrypted project data and the same master password")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"success": True, "package": PACKAGE, "framework": "Kotlin / Jetpack Compose", "checks": checks}, indent=2))
    print("Signed native release installation and data-preserving reinstall passed.")
except Exception as error:
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"success": False, "error": str(error), "checks": checks}, indent=2))
    for name, command in [
        ("failure-window.xml", ("shell", "cat", "/sdcard/moji-release-window.xml")),
        ("failure-runtime.txt", ("logcat", "-d", "-v", "threadtime", "-s", "AndroidRuntime")),
        ("failure-activity.txt", ("shell", "dumpsys", "activity", "activities")),
    ]:
        try:
            (REPORT.parent / name).write_text(adb(*command))
        except Exception:
            pass
    raise
