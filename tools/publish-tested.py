"""Publish successful native CI artifacts without rebuilding or accessing signing keys."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

platform = os.environ["MOJI_PLATFORM"]
sha = os.environ["MOJI_TESTED_SHA"]
run_id = os.environ["MOJI_RUN_ID"]
repository = os.environ["GITHUB_REPOSITORY"]

def source(path):
    return subprocess.check_output(["git", "show", f"{sha}:{path}"], text=True)

def checked_report(path):
    report = json.loads(path.read_text(encoding="utf-8-sig"))
    if report.get("success") is not True:
        raise RuntimeError("Native verification report is not successful")
    return report

if platform == "windows":
    version = re.search(r"<Version>([0-9.]+)</Version>", source("src/Moji.App/Moji.App.csproj")).group(1)
    folder = Path("verified/windows/release")
    checked_report(folder / "native-test-report.json")
    sums = json.loads((folder / "SHA256SUMS.json").read_text(encoding="utf-8-sig"))
    for architecture in ("x64", "arm64"):
        name = f"Moji-Notes-{version}-win-{architecture}.exe"
        if hashlib.sha256((folder / name).read_bytes()).hexdigest() != sums[name]:
            raise RuntimeError("Windows executable checksum mismatch")
    for capture, name in [("08-notebook-appearance.png", "Moji-Notebook-Appearance.png"), ("09-custom-notebook-home.png", "Moji-Custom-Notebook.png")]:
        shutil.copy(folder.parent / "smoke" / capture, folder / name)
    tag, title, notes = f"v{version}", f"墨记 Windows {version}", source("docs/RELEASE.md")
else:
    version = re.search(r'versionName = "([0-9.]+)"', source("android/app/build.gradle.kts")).group(1)
    build = Path("verified/android/outputs/apk/release")
    ui = Path("verified/android-ui")
    report_path = ui / "android/app/build/outputs/release-smoke/report.json"
    checked_report(report_path)
    certificate = (build / "SIGNING-CERTIFICATE-SHA256.txt").read_text().strip().lower()
    if certificate != "4c74ca5b73e47d9435c7f0fcc68edd2e56b9923177b65f33a32e92d67e661198":
        raise RuntimeError("Android publisher certificate mismatch")
    folder = Path("verified/android-release")
    folder.mkdir()
    shutil.copy(build / "app-release.apk", folder / f"Moji-Notes-Android-{version}.apk")
    shutil.copy(build / "SIGNING-CERTIFICATE-SHA256.txt", folder)
    shutil.copy(report_path, folder / "native-release-test-report.json")
    for capture in (ui / "android-ui-screenshots").glob("*.png"):
        shutil.copy(capture, folder)
    subprocess.run(["git", "archive", "--format=zip", f"--output={folder}/Moji-Notes-Android-{version}-source.zip", sha], check=True)
    tag, title, notes = f"android-v{version}", f"墨记 Android {version}", source("android/RELEASE.md")

checksums = [hashlib.sha256(file.read_bytes()).hexdigest() + "  " + file.name for file in sorted(folder.iterdir()) if file.suffix in (".exe", ".apk", ".zip")]
(folder / "SHA256SUMS.txt").write_text("\n".join(checksums) + "\n")
(folder / "BUILD-PROVENANCE.json").write_text(json.dumps({"commit": sha, "workflow_run": f"https://github.com/{repository}/actions/runs/{run_id}", "platform": platform}, indent=2) + "\n")
notes_file = Path(os.environ["RUNNER_TEMP"]) / "moji-release-notes.txt"
notes_file.write_text(notes)
subprocess.run(["gh", "release", "create", tag, *[str(file) for file in sorted(folder.iterdir()) if file.is_file()], "--target", sha, "--title", title, "--notes-file", str(notes_file)], check=True)
