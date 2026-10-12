# Downloads the MediaPipe Unity plugin tarball that Packages/manifest.json references.
$version = '0.16.3'
$name = "com.github.homuler.mediapipe-$version.tgz"
$target = Join-Path $PSScriptRoot "..\Packages\$name"

if (Test-Path $target) {
    Write-Host "$name already present"
    exit 0
}

$url = "https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v$version/$name"
Write-Host "Downloading $url"
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri $url -OutFile $target
