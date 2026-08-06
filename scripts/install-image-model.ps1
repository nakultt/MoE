[CmdletBinding()]
param(
    [string]$ModelDirectory = 'E:\Models\flux2-klein'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command hf -ErrorAction SilentlyContinue)) {
    throw 'Hugging Face CLI was not found. Install it with: uv tool install huggingface-hub'
}

New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null
hf download leejet/FLUX.2-klein-4B-GGUF flux-2-klein-4b-Q4_0.gguf --local-dir $ModelDirectory
hf download unsloth/Qwen3-4B-GGUF Qwen3-4B-Q4_K_M.gguf --local-dir $ModelDirectory
hf download Comfy-Org/flux2-klein-4B split_files/vae/flux2-vae.safetensors --local-dir $ModelDirectory

$downloadedVae = Join-Path $ModelDirectory 'split_files\vae\flux2-vae.safetensors'
$targetVae = Join-Path $ModelDirectory 'flux2-vae.safetensors'
if (Test-Path -LiteralPath $downloadedVae) {
    Move-Item -LiteralPath $downloadedVae -Destination $targetVae -Force
}

Write-Host "FLUX.2 Klein model files installed in $ModelDirectory"
Write-Host 'If this is not the configured path, update Configuration\image-model.json.'
