[CmdletBinding()]
param(
    [string]$ModelDirectory = 'E:\Models\flux3-klein',

    # The FLUX.2 VAE is shared by FLUX.3 Image Klein; reuse an existing copy when present.
    [string]$VaePath = 'E:\Models\flux2-klein\flux2-vae.safetensors'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command hf -ErrorAction SilentlyContinue)) {
    throw 'Hugging Face CLI was not found. Install it with: uv tool install huggingface-hub'
}

New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null

# FLUX.3 Image Klein (distilled, 16 steps) Q4_K diffusion transformer (~3.5 GB), converted by the
# stable-diffusion.cpp author. Needs stable-diffusion.cpp master-968 or newer.
hf download leejet/flux-3-image-klein-GGUF flux3-image-klein-Q4_K.gguf --local-dir $ModelDirectory
if ($LASTEXITCODE -ne 0) { throw 'FLUX.3 Image Klein download failed.' }

# Qwen3-VL 4B text encoder Q4_K_M (~2.5 GB).
hf download Qwen/Qwen3-VL-4B-Instruct-GGUF Qwen3VL-4B-Instruct-Q4_K_M.gguf --local-dir $ModelDirectory
if ($LASTEXITCODE -ne 0) { throw 'Qwen3-VL 4B text encoder download failed.' }

if (-not (Test-Path -LiteralPath $VaePath)) {
    $vaeDirectory = Split-Path $VaePath -Parent
    New-Item -ItemType Directory -Force -Path $vaeDirectory | Out-Null
    hf download Comfy-Org/flux2-klein-4B split_files/vae/flux2-vae.safetensors --local-dir $vaeDirectory
    if ($LASTEXITCODE -ne 0) { throw 'FLUX.2 VAE download failed.' }
    Move-Item -LiteralPath (Join-Path $vaeDirectory 'split_files\vae\flux2-vae.safetensors') -Destination $VaePath -Force
}

Write-Host "FLUX.3 Image Klein model files installed in $ModelDirectory (VAE: $VaePath)"
Write-Host 'If this is not the configured path, update Configuration\image-models.json.'
