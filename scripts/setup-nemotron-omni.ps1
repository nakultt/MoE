[CmdletBinding()]
param(
    [string]$Destination = 'E:\Models\Nemotron-3-Nano-Omni-30B-A3B'
)

$ErrorActionPreference = 'Stop'
$repository = 'mudler/Nemotron-3-Nano-Omni-30B-A3B-Reasoning-APEX-GGUF'
$officialRepository = 'nvidia/Nemotron-3-Nano-Omni-30B-A3B-Reasoning-BF16'
$modelFile = 'Nemotron-3-Nano-Omni-30B-A3B-Reasoning-APEX-I-Nano.gguf'
$projectorFile = 'mmproj-combined-F16.gguf'
$officialSourceDirectory = Join-Path $Destination 'official-media-source'
$repoRoot = Split-Path $PSScriptRoot -Parent
$llamaSourceDirectory = Join-Path $repoRoot 'artifacts\llama.cpp-b10359-source'

if (-not (Get-Command hf -ErrorAction SilentlyContinue)) {
    throw 'The Hugging Face CLI (hf) is required. Install it with: pip install -U huggingface_hub'
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Write-Host 'Downloading the low-memory community GGUF derived from NVIDIA Nemotron 3 Nano Omni.'
Write-Host 'The official NVIDIA FP8/NVFP4 checkpoint does not fit an 8 GB RTX 4060.'
& hf download $repository $modelFile --local-dir $Destination
if ($LASTEXITCODE -ne 0) {
    throw "Hugging Face download failed with exit code $LASTEXITCODE."
}

Write-Host 'Downloading NVIDIA BF16 media tensors required for a genuine vision+audio projector.'
& hf download $officialRepository `
    'model-00001-of-00017.safetensors' `
    'config.json' `
    'preprocessor_config.json' `
    --local-dir $officialSourceDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Official media-shard download failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath (Join-Path $llamaSourceDirectory 'convert_hf_to_gguf.py'))) {
    New-Item -ItemType Directory -Path (Split-Path $llamaSourceDirectory -Parent) -Force | Out-Null
    & git clone --depth 1 --branch b10359 https://github.com/ggml-org/llama.cpp.git $llamaSourceDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "llama.cpp source download failed with exit code $LASTEXITCODE."
    }
}
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    throw 'uv is required to build the combined projector. Install it from https://docs.astral.sh/uv/.'
}

Write-Host 'Building combined CRADIO vision + Parakeet audio projector from NVIDIA tensors.'
Push-Location $llamaSourceDirectory
try {
    & uv run `
        --with torch `
        --with transformers `
        --with sentencepiece `
        --with protobuf `
        python convert_hf_to_gguf.py `
        --mmproj `
        --outtype f16 `
        --outfile (Join-Path $Destination $projectorFile) `
        $officialSourceDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Combined projector conversion failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

foreach ($file in @($modelFile, $projectorFile)) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destination $file))) {
        throw "Download completed without required file: $file"
    }
}

Write-Host "Nemotron Omni files are ready at $Destination"
