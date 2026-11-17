#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
inkscape "$repo_dir/design/brand/moji-icon.svg" --export-type=png --export-width=512 --export-height=512 --export-filename="$repo_dir/assets/icon.png"
magick "$repo_dir/assets/icon.png" -strip -define icon:auto-resize=256,128,96,64,48,40,32,24,20,16 "$repo_dir/assets/icon.ico"
cp "$repo_dir/assets/icon.png" "$repo_dir/android/app/src/main/res/drawable/moji_icon.png"
