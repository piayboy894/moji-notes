"""Publish tiny public update feeds from already-published, verified release assets.

No credentials are placed in the feeds. Desktop/mobile clients only read HTTPS.
The repository copy and a separate prerelease asset provide two delivery paths.
"""
import argparse
import base64
import json
from pathlib import Path
import re
import subprocess
import tempfile
import time

REPOSITORY = "piayboy894/moji-notes"


def api(path, method="GET", data=None, missing=False):
    result = subprocess.run(["gh", "api", path, "--method", method] + (["--input", "-"] if data is not None else []),
                            input=json.dumps(data) if data is not None else None, encoding="utf-8", capture_output=True)
    if result.returncode:
        if missing and "HTTP 404" in result.stderr:
            return None
        raise RuntimeError(result.stderr.strip())
    return json.loads(result.stdout) if result.stdout.strip() else None


def version_tuple(version):
    if not re.fullmatch(r"\d{1,3}\.\d{1,2}\.\d{1,2}", version):
        raise ValueError("Unsupported stable version")
    return tuple(map(int, version.split(".")))


def build_feed(platform, release):
    prefix = "v" if platform == "windows" else "android-v"
    tag = release["tag_name"]
    if release["draft"] or release["prerelease"] or not tag.startswith(prefix):
        raise ValueError("Only public stable releases can update the feed")
    version = tag[len(prefix):]
    version_tuple(version)
    names = [f"Moji-Notes-{version}-win-{arch}.exe" for arch in ("x64", "arm64")] if platform == "windows" else [f"Moji-Notes-Android-{version}.apk"]
    indexed = {asset["name"]: asset for asset in release["assets"]}
    assets = []
    for name in names:
        asset = indexed[name]
        expected = f"https://github.com/{REPOSITORY}/releases/download/{tag}/{name}"
        if asset["browser_download_url"] != expected or asset["size"] <= 0 or not re.fullmatch(r"sha256:[a-f0-9]{64}", asset.get("digest") or ""):
            raise ValueError("Release asset has no trusted URL, size or SHA-256")
        assets.append(dict(name=name, url=expected, size=asset["size"], sha256=asset["digest"][7:]))
    return dict(schema=1, platform=platform, version=version, release_url=f"https://github.com/{REPOSITORY}/releases/tag/{tag}", assets=assets)


def publish(platform, feed):
    content = json.dumps(feed, ensure_ascii=False, separators=(",", ":")) + "\n"
    path = f"repos/{REPOSITORY}/contents/updates/{platform}.json"
    for attempt in range(4):
        old = api(path, missing=True)
        if old:
            previous = json.loads(base64.b64decode(old["content"]))
            if version_tuple(previous["version"]) > version_tuple(feed["version"]):
                raise ValueError("Refusing to downgrade a public update channel")
        data = dict(message=f"Update {platform} public feed to {feed['version']} [skip ci]", branch="main", content=base64.b64encode(content.encode()).decode())
        if old:
            data["sha"] = old["sha"]
        try:
            if not old or base64.b64decode(old["content"]).decode() != content:
                api(path, "PUT", data)
            break
        except RuntimeError:
            if attempt == 3:
                raise
            time.sleep(attempt + 1)
    channel_path = f"repos/{REPOSITORY}/releases/tags/update-channel"
    if api(channel_path, missing=True) is None:
        created = subprocess.run(["gh", "release", "create", "update-channel", "--repo", REPOSITORY, "--target", "main", "--prerelease", "--latest=false", "--title", "公开更新清单", "--notes", "Windows 与 Android 公开版本清单。此预发布仅提供更新元数据，不作为软件版本。"], capture_output=True, encoding="utf-8")
        if created.returncode and api(channel_path, missing=True) is None:
            raise RuntimeError(created.stderr)
    with tempfile.TemporaryDirectory(prefix="moji-update-feed-") as temporary:
        file = Path(temporary) / f"{platform}.json"
        file.write_text(content, encoding="utf-8")
        subprocess.run(["gh", "release", "upload", "update-channel", str(file), "--repo", REPOSITORY, "--clobber"], check=True)
    print(f"Published {platform} {feed['version']} update feed ({len(content.encode())} bytes) through both official GitHub paths.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--platform", choices=("windows", "android"), required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--publish", action="store_true")
    parser.add_argument("--output")
    args = parser.parse_args()
    feed = build_feed(args.platform, api(f"repos/{REPOSITORY}/releases/tags/{args.tag}"))
    if args.output:
        target = Path(args.output)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(feed, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    if args.publish:
        publish(args.platform, feed)
