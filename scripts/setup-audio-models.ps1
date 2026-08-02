[CmdletBinding()]
param(
    [string]$ModelRoot = "C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models"
)

$ErrorActionPreference = "Stop"
$downloads = @(
    @{
        Name = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8"
        Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2"
    },
    @{
        Name = "kokoro-multi-lang-v1_0"
        Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2"
    }
)

New-Item -ItemType Directory -Force -Path $ModelRoot | Out-Null
foreach ($download in $downloads) {
    $targetDirectory = Join-Path $ModelRoot $download.Name
    if (Test-Path -LiteralPath $targetDirectory) {
        Write-Host "$($download.Name) is already installed."
        continue
    }

    $archivePath = Join-Path $ModelRoot "$($download.Name).tar.bz2"
    Write-Host "Downloading $($download.Name)..."
    & curl.exe --fail --location --continue-at - --output $archivePath $download.Url
    if ($LASTEXITCODE -ne 0) {
        throw "Download failed for $($download.Name)."
    }

    Write-Host "Extracting $($download.Name)..."
    & tar.exe -xjf $archivePath -C $ModelRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Extraction failed for $($download.Name)."
    }
}

Write-Host "Audio models are ready under $ModelRoot."
