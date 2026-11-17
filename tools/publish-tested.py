"""Publish successful native CI artifacts without rebuilding or accessing signing keys."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

platform = os.environ["MOJI_PLATFORM"]
reused = {}
sha = os.environ["MOJI_TESTED_SHA"]
run_id = os.environ["MOJI_RUN_ID"]
repository = os.environ["GITHUB_REPOSITORY"]
replace_existing = os.environ.get("MOJI_REPLACE_EXISTING") == "true"

def api(endpoint, method="GET", data=None):
    command = ["gh", "api", endpoint, "--method", method]
    if data is not None:
        command += ["--input", "-"]
    result = subprocess.check_output(command, input=json.dumps(data) if data is not None else None, text=True)
    return json.loads(result) if result.strip() else None

def digest(file):
    with file.open("rb") as stream:
        return hashlib.file_digest(stream,"sha256").hexdigest()

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
    standard = checked_report(folder / "native-standard-test-report.json")
    checked_report(folder / "standard-launch-report.json")
    if standard.get("bootstrapperInvocations") != 0 or standard.get("hasFixedRuntime") is not False:
        raise RuntimeError("Standard package did not reuse the installed shared runtime")
    sums = json.loads((folder / "SHA256SUMS.json").read_text(encoding="utf-8-sig"))
    for architecture in ("x64", "arm64"):
        name = f"Moji-Notes-{version}-win-{architecture}.exe"
        if hashlib.sha256((folder / name).read_bytes()).hexdigest() != sums[name]:
            raise RuntimeError("Windows executable checksum mismatch")
    tag, title, notes = f"v{version}", f"墨记 Windows {version}", source("docs/RELEASE.md")
else:
    reuse_path=Path('verified/android/REUSED-BUILD.json')
    if reuse_path.exists():
        reused=json.loads(reuse_path.read_text())
        if reused.get('verification_commit')!=sha:raise RuntimeError('Verification provenance does not match this run')
        subprocess.run(['git','diff','--exit-code',reused['binary_source_commit'],sha,'--','android/app','android/build.gradle.kts','android/settings.gradle.kts','android/gradle.properties','android/gradle','android/gradlew','android/gradlew.bat','editor','assets'],check=True)
    version = re.search(r'versionName = "([0-9.]+)"', source("android/app/build.gradle.kts")).group(1)
    build = Path("verified/android/outputs/apk/release")
    ui = Path("verified/android-ui")
    report_path = ui / "android/app/build/outputs/release-smoke/report.json"
    checked_report(report_path)
    editor_report=ui / "android/app/build/outputs/editor-smoke/report.json"
    checked_report(editor_report)
    certificate = (build / "SIGNING-CERTIFICATE-SHA256.txt").read_text().strip().lower()
    if certificate != "4c74ca5b73e47d9435c7f0fcc68edd2e56b9923177b65f33a32e92d67e661198":
        raise RuntimeError("Android publisher certificate mismatch")
    folder = Path("verified/android-release")
    folder.mkdir()
    shutil.copy(build / "app-release.apk", folder / f"Moji-Notes-Android-{version}.apk")
    shutil.copy(build / "SIGNING-CERTIFICATE-SHA256.txt", folder)
    shutil.copy(report_path, folder / "native-release-test-report.json")
    shutil.copy(editor_report,folder / "native-editor-test-report.json")
    for capture in (ui / "android-ui-screenshots").glob("*.png"):
        shutil.copy(capture, folder)
    subprocess.run(["git", "archive", "--format=zip", f"--output={folder}/Moji-Notes-Android-{version}-source.zip", sha], check=True)
    import zipfile
    archive_path=folder / f"Moji-Notes-Android-{version}-source.zip"
    with zipfile.ZipFile(archive_path) as archive:
        clean=[(item,archive.read(item.filename)) for item in archive.infolist() if not item.filename.endswith("_wpftmp.csproj")]
    with zipfile.ZipFile(archive_path,"w",compression=zipfile.ZIP_DEFLATED) as archive:
        for item,data in clean:archive.writestr(item,data)
    tag, title, notes = f"android-v{version}", f"墨记 Android {version}", source("android/RELEASE.md")

preserved = []
release = None
if replace_existing:
    release = api(f"repos/{repository}/releases/tags/{tag}")
    if release.get("immutable") or release["draft"] or release["prerelease"]:
        raise RuntimeError("Only an existing mutable public release can be replaced")
    assets = {asset["name"]: asset for asset in release["assets"]}
    if platform == "windows":
        preserve_names = {
            f"Moji-Notes-{version}-win-{arch}.{ext}": f"Moji-Notes-{version}-win-{arch}-offline.{ext}"
            for arch in ("x64","arm64") for ext in ("exe","zip")
        }
        preserve_names.update({f"Moji-Notes-{version}-source.zip": f"Moji-Notes-{version}-offline-source.zip",
            "native-test-report.json": "native-test-report-offline.json", "BUILD-PROVENANCE.json": "BUILD-PROVENANCE-offline.json"})
        for arch in ("x64","arm64"):
            name=f"Moji-Notes-{version}-win-{arch}.exe"
            old=assets.get(preserve_names[name],assets.get(name))
            if old is None or old["size"] < 200_000_000:
                raise RuntimeError("Expected the verified complete offline package before replacing it")
    else:
        preserve_names = {f"Moji-Notes-Android-{version}.{ext}": f"Moji-Notes-Android-{version}-previous.{ext}" for ext in ("apk",)}
        preserve_names.update({f"Moji-Notes-Android-{version}-source.zip": f"Moji-Notes-Android-{version}-previous-source.zip",
            "native-release-test-report.json": "native-release-test-report-previous.json", "native-editor-test-report.json": "native-editor-test-report-previous.json",
            "BUILD-PROVENANCE.json": "BUILD-PROVENANCE-previous.json"})
    for old_name, saved_name in preserve_names.items():
        asset=assets.get(saved_name,assets.get(old_name))
        if asset is not None:
            if not re.fullmatch(r"sha256:[0-9a-f]{64}",asset.get("digest") or ""):
                raise RuntimeError("Existing asset has no verifiable GitHub digest")
            preserved.append({"id":asset["id"],"name":saved_name,"original_name":asset["name"],"size":asset["size"],"sha256":asset["digest"][7:]})

all_sums={file.name:digest(file) for file in sorted(folder.iterdir()) if file.suffix in (".exe", ".apk", ".zip")}
all_sums.update({asset["name"]:asset["sha256"] for asset in preserved if Path(asset["name"]).suffix in (".exe",".apk",".zip")})
(folder / "SHA256SUMS.txt").write_text("".join(f"{value}  {name}\n" for name,value in sorted(all_sums.items())))
if platform == "windows":
    (folder / "SHA256SUMS.json").write_text(json.dumps(all_sums,indent=2)+"\n")
# A GITHUB_TOKEN cannot create an old tag across intervening workflow changes.
# Android may use a later release tag only after verifying that every APK input
# and native test is byte-identical. The binary/source archive keep the tested SHA.
release_target = sha
if platform == "android" and not replace_existing:
    release_target = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    subprocess.run(["git", "diff", "--exit-code", sha, release_target, "--",
        "android/app", "android/build.gradle.kts", "android/settings.gradle.kts",
        "android/gradle.properties", "android/gradle", "android/gradlew", "android/gradlew.bat",
        "editor", "assets", ".github/workflows/android.yml"], check=True)
(folder / "BUILD-PROVENANCE.json").write_text(json.dumps({"commit": sha, "tag_commit": release_target, "workflow_run": f"https://github.com/{repository}/actions/runs/{run_id}", "platform": platform, "preserved_assets":preserved, **reused}, indent=2) + "\n")
notes_file = Path(os.environ["RUNNER_TEMP"]) / "moji-release-notes.txt"
notes_file.write_text(notes)
files=[file for file in sorted(folder.iterdir()) if file.is_file()]
if not replace_existing:
    subprocess.run(["gh", "release", "create", tag, *map(str,files), "--target", release_target, "--title", title, "--notes-file", str(notes_file)], check=True)
else:
    # Upload and verify everything before touching any current download. Keep
    # the original tag and the independently tested complete/previous packages.
    with tempfile.TemporaryDirectory(prefix="moji-release-stage-") as temporary:
        staged={file.name:Path(temporary)/f"repack-{run_id}-{file.name}" for file in files}
        for file in files:shutil.copyfile(file,staged[file.name])
        subprocess.run(["gh","release","upload",tag,*map(str,staged.values()),"--clobber"],check=True)
        uploaded={asset["name"]:asset for asset in api(f"repos/{repository}/releases/{release['id']}/assets?per_page=100")}
        for file in files:
            asset=uploaded[staged[file.name].name]
            if asset["size"]!=file.stat().st_size or asset["digest"]!="sha256:"+digest(file):
                raise RuntimeError("Staged release asset does not match the tested local bytes")
        for asset in preserved:
            if asset["original_name"]!=asset["name"]:
                api(f"repos/{repository}/releases/assets/{asset['id']}","PATCH",{"name":asset["name"]})
        current={asset["name"]:asset for asset in api(f"repos/{repository}/releases/{release['id']}/assets?per_page=100")}
        # Publish checksums last. A download spanning the brief rename window
        # may fail validation and retry; unverified bytes are never executable.
        for file in sorted(files,key=lambda file:file.name.startswith("SHA256SUMS")):
            if file.name in current:api(f"repos/{repository}/releases/assets/{current[file.name]['id']}","DELETE")
            api(f"repos/{repository}/releases/assets/{uploaded[staged[file.name].name]['id']}","PATCH",{"name":file.name})
        subprocess.run(["gh","release","edit",tag,"--title",title,"--notes-file",str(notes_file)],check=True)
        final={asset["name"]:asset for asset in api(f"repos/{repository}/releases/{release['id']}/assets?per_page=100")}
        for file in files:
            if final[file.name]["digest"]!="sha256:"+digest(file):raise RuntimeError("Final release verification failed")
        for asset in preserved:
            if final[asset["name"]]["digest"]!="sha256:"+asset["sha256"]:raise RuntimeError("Preserved asset changed")
    print(f"Verified replacement of {tag}; preserved {len(preserved)} original assets. Tag commit unchanged.")
