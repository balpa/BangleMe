#!/bin/sh
# Downloads the MediaPipe Unity plugin tarball that Packages/manifest.json references.
set -e
version=0.16.3
name="com.github.homuler.mediapipe-$version.tgz"
target="$(dirname "$0")/../Packages/$name"

if [ -f "$target" ]; then
    echo "$name already present"
    exit 0
fi

url="https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v$version/$name"
echo "Downloading $url"
curl -fL -o "$target" "$url"
