[CmdletBinding()]
param(
    [string]$Distribution = 'Ubuntu',
    [string]$ModelDirectory = 'C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models\fastwan2.2-5b',
    [switch]$SkipModelDownload
)

$ErrorActionPreference = 'Stop'
$fastVideoRevision = '1b2b2a0161bc6b3b80158d1fa6380a051c6530c7'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeDirectory = Join-Path $repositoryRoot 'runtime\fastvideo'

function ConvertTo-WslPath {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.Length -lt 3 -or $fullPath[1] -ne ':') {
        throw "Only drive-letter Windows paths are supported: $Path"
    }

    $drive = [char]::ToLowerInvariant($fullPath[0])
    $remainder = $fullPath.Substring(3).Replace('\', '/')
    return "/mnt/$drive/$remainder"
}

if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
    throw 'WSL is required. Install WSL and Ubuntu before running this script.'
}

if (-not (Test-Path -LiteralPath (Join-Path $runtimeDirectory '.git'))) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $runtimeDirectory) | Out-Null
    git clone https://github.com/hao-ai-lab/FastVideo.git $runtimeDirectory
}

git -C $runtimeDirectory fetch origin $fastVideoRevision
git -C $runtimeDirectory checkout --detach $fastVideoRevision

$wslRuntimeDirectory = ConvertTo-WslPath $runtimeDirectory
$setupCommand = @"
set -euo pipefail
if ! command -v uv >/dev/null 2>&1; then
  curl -LsSf https://astral.sh/uv/install.sh | sh
fi
UV_BIN=`$(command -v uv || printf '%s' /home/nakul/.local/bin/uv)
"`$UV_BIN" venv --python 3.12 --seed /home/nakul/.venvs/moe-fastvideo
"`$UV_BIN" pip install --python /home/nakul/.venvs/moe-fastvideo/bin/python -e '$wslRuntimeDirectory'
/home/nakul/.venvs/moe-fastvideo/bin/python -c "import torch; print(torch.__version__); print(torch.cuda.get_device_name(0))"
"@

& wsl.exe --distribution $Distribution --exec /bin/bash -lc $setupCommand
if ($LASTEXITCODE -ne 0) {
    throw "FastVideo setup failed with exit code $LASTEXITCODE."
}

if (-not $SkipModelDownload) {
    if (-not (Get-Command hf -ErrorAction SilentlyContinue)) {
        throw 'The Hugging Face CLI is required. Install it, then rerun this script.'
    }

    New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null
    $env:HF_HUB_DISABLE_XET = '1'
    hf download FastVideo/FastWan2.2-TI2V-5B-FullAttn-Diffusers `
        --local-dir $ModelDirectory `
        --max-workers 1
    if ($LASTEXITCODE -ne 0) {
        throw "Video model download failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'FastVideo runtime setup is complete.' -ForegroundColor Green
