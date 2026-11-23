[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Models'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command hf -ErrorAction SilentlyContinue)) {
    throw 'Hugging Face CLI was not found. Install it with: uv tool install huggingface-hub'
}

# ---------------------------------------------------------------------------
# Qwen3.6 35B A3B — main model + vision projector
# ---------------------------------------------------------------------------
$qwenDir = $ModelRoot
Write-Host "`n=== Qwen3.6 35B A3B ===" -ForegroundColor Cyan

# Main Q4_K_M GGUF (bartowski quantization, ~21 GB)
$qwenModel = Join-Path $qwenDir 'Qwen_Qwen3.6-35B-A3B-Q4_K_M.gguf'
if (Test-Path -LiteralPath $qwenModel) {
    Write-Host 'Qwen3.6 Q4_K_M GGUF already present.'
} else {
    Write-Host 'Downloading Qwen3.6 Q4_K_M GGUF (~21 GB)...'
    hf download bartowski/Qwen_Qwen3.6-35B-A3B-GGUF Qwen_Qwen3.6-35B-A3B-Q4_K_M.gguf --local-dir $qwenDir
    if ($LASTEXITCODE -ne 0) { throw 'Qwen model download failed.' }
}

# Vision projector Q8_0 (~586 MB)
$qwenMmproj = Join-Path $qwenDir 'mmproj-Qwen3.6-35B-A3B-Q8_0.gguf'
if (Test-Path -LiteralPath $qwenMmproj) {
    Write-Host 'Qwen3.6 vision projector already present.'
} else {
    Write-Host 'Downloading Qwen3.6 vision projector Q8_0 (~586 MB)...'
    hf download bartowski/Qwen_Qwen3.6-35B-A3B-GGUF mmproj-Qwen3.6-35B-A3B-Q8_0.gguf --local-dir $qwenDir
    if ($LASTEXITCODE -ne 0) { throw 'Qwen vision projector download failed.' }
}

# ---------------------------------------------------------------------------
# Gemma 4 26B A4B — main model + vision projector + MTP draft models
# ---------------------------------------------------------------------------
$gemmaDir = Join-Path $ModelRoot 'gemma4'
$gemmaMtpDir = Join-Path $gemmaDir 'MTP'
New-Item -ItemType Directory -Force -Path $gemmaDir, $gemmaMtpDir | Out-Null
Write-Host "`n=== Gemma 4 26B A4B ===" -ForegroundColor Cyan

# Main QAT UD-Q4_K_XL GGUF (unsloth quantization, ~13.6 GB)
$gemmaModel = Join-Path $gemmaDir 'gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf'
if (Test-Path -LiteralPath $gemmaModel) {
    Write-Host 'Gemma 4 QAT UD-Q4_K_XL GGUF already present.'
} else {
    Write-Host 'Downloading Gemma 4 QAT UD-Q4_K_XL GGUF (~13.6 GB)...'
    hf download unsloth/gemma-4-26B-A4B-it-qat-GGUF gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf --local-dir $gemmaDir
    if ($LASTEXITCODE -ne 0) { throw 'Gemma model download failed.' }
}

# Vision projector F16 (~1.1 GB)
$gemmaMmproj = Join-Path $gemmaDir 'mmproj-F16.gguf'
if (Test-Path -LiteralPath $gemmaMmproj) {
    Write-Host 'Gemma 4 vision projector already present.'
} else {
    Write-Host 'Downloading Gemma 4 vision projector F16 (~1.1 GB)...'
    hf download unsloth/gemma-4-26B-A4B-it-qat-GGUF mmproj-F16.gguf --local-dir $gemmaDir
    if ($LASTEXITCODE -ne 0) { throw 'Gemma vision projector download failed.' }
}

# MTP draft model Q4 (~240 MB, in gemma4 root for benchmarking)
$gemmaMtpQ4 = Join-Path $gemmaDir 'mtp-gemma-4-26B-A4B-it.gguf'
if (Test-Path -LiteralPath $gemmaMtpQ4) {
    Write-Host 'Gemma 4 MTP Q4 draft model already present.'
} else {
    Write-Host 'Downloading Gemma 4 MTP Q4 draft model (~240 MB)...'
    hf download unsloth/gemma-4-26B-A4B-it-qat-GGUF mtp-gemma-4-26B-A4B-it.gguf --local-dir $gemmaDir
    if ($LASTEXITCODE -ne 0) { throw 'Gemma MTP Q4 download failed.' }
}

# MTP draft model Q8_0 (~440 MB, in gemma4\MTP — the one used in production)
$gemmaMtpQ8 = Join-Path $gemmaMtpDir 'mtp-gemma-4-26B-A4B-it-Q8_0.gguf'
if (Test-Path -LiteralPath $gemmaMtpQ8) {
    Write-Host 'Gemma 4 MTP Q8_0 draft model already present.'
} else {
    Write-Host 'Downloading Gemma 4 MTP Q8_0 draft model (~440 MB)...'
    hf download unsloth/gemma-4-26B-A4B-it-qat-GGUF mtp-gemma-4-26B-A4B-it-Q8_0.gguf --local-dir $gemmaMtpDir
    if ($LASTEXITCODE -ne 0) { throw 'Gemma MTP Q8_0 download failed.' }
}

# ---------------------------------------------------------------------------
# Inkling 2 Mini (Thinking Machines) — mainline-layout UD-Q4_K_XL + vision projector
# ---------------------------------------------------------------------------
$inklingDir = Join-Path $ModelRoot 'Inkling-2-Mini'
New-Item -ItemType Directory -Force -Path $inklingDir | Out-Null
Write-Host "`n=== Inkling 2 Mini ===" -ForegroundColor Cyan

# Unsloth's root GGUFs use a looped layout only Unsloth's llama.cpp fork reads. The Mainline_Support copies
# hold the same tensors in the inkling2_loop layout that ggml-org llama.cpp b12611+ reads; they ship as
# .gguf_file and must be renamed (~17.6 GB).
$inklingModel = Join-Path $inklingDir 'Inkling-2-Mini-UD-Q4_K_XL.gguf'
if (Test-Path -LiteralPath $inklingModel) {
    Write-Host 'Inkling 2 Mini UD-Q4_K_XL GGUF already present.'
} else {
    Write-Host 'Downloading Inkling 2 Mini UD-Q4_K_XL mainline GGUF (~17.6 GB)...'
    hf download unsloth/Inkling-2-Mini-GGUF Mainline_Support/Inkling-2-Mini-UD-Q4_K_XL.gguf_file --local-dir $inklingDir
    if ($LASTEXITCODE -ne 0) { throw 'Inkling model download failed.' }
    Move-Item -LiteralPath (Join-Path $inklingDir 'Mainline_Support\Inkling-2-Mini-UD-Q4_K_XL.gguf_file') -Destination $inklingModel
    Remove-Item -LiteralPath (Join-Path $inklingDir 'Mainline_Support') -Recurse -Force
}

# Vision projector F16 (~640 MB)
$inklingMmproj = Join-Path $inklingDir 'mmproj-F16.gguf'
if (Test-Path -LiteralPath $inklingMmproj) {
    Write-Host 'Inkling 2 Mini vision projector already present.'
} else {
    Write-Host 'Downloading Inkling 2 Mini vision projector F16 (~640 MB)...'
    hf download unsloth/Inkling-2-Mini-GGUF mmproj-F16.gguf --local-dir $inklingDir
    if ($LASTEXITCODE -ne 0) { throw 'Inkling vision projector download failed.' }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host "`n=== Download summary ===" -ForegroundColor Green
Write-Host "Qwen3.6 model:     $qwenModel"
Write-Host "Qwen3.6 projector: $qwenMmproj"
Write-Host "Gemma 4 model:     $gemmaModel"
Write-Host "Gemma 4 projector: $gemmaMmproj"
Write-Host "Gemma 4 MTP Q4:    $gemmaMtpQ4"
Write-Host "Gemma 4 MTP Q8:    $gemmaMtpQ8"
Write-Host "Inkling model:     $inklingModel"
Write-Host "Inkling projector: $inklingMmproj"
Write-Host ''
Write-Host 'If these paths do not match Configuration\models.json, update the JSON file.'
