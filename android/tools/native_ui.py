#!/usr/bin/env python3
"""Exercise the signed native release using Android's UI hierarchy and installer."""
import hashlib
import urllib.request
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
REPORT = Path("app/build/outputs/editor-smoke/report.json")
checks = []

def adb(*args):
    result = subprocess.run(["adb", *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=70)
    if result.returncode:
        raise RuntimeError("Android command failed: " + " ".join(args[:3]))
    return result.stdout

def hierarchy():
    adb("shell", "uiautomator", "dump", "/sdcard/moji-release-window.xml")
    return ET.fromstring(adb("shell", "cat", "/sdcard/moji-release-window.xml"))

def wait_node(resource=None, text=None, contains=None, description=None, timeout=45):
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
                if description is not None and node.attrib.get("content-desc") != description:
                    continue
                if not resource and text is None and contains is None and description is None:
                    continue
                return node
        except (ET.ParseError, RuntimeError):
            pass
        time.sleep(.6)
    raise RuntimeError("Native release UI did not reach: " + str(resource or text or contains or description))

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

