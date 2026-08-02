param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$pinnedRevision = 'e31a86c'
$runtimeDir = Join-Path $repoRoot 'runtime\stable-diffusion.cpp'
$buildDir = Join-Path $repoRoot 'work\sd-build'
$sourceDir = Join-Path $buildDir 'stable-diffusion.cpp'

if (Test-Path -LiteralPath (Join-Path $runtimeDir 'sd-server.exe')) {
    if (-not $Force) {
        Write-Host "Image runtime is already installed at $runtimeDir"
        Write-Host 'Use -Force only if you intentionally want to rebuild it.'
        exit 0
    }
    Remove-Item -LiteralPath $runtimeDir -Recurse -Force
}

# Validate build tools
foreach ($tool in @('git', 'cmake')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is required but was not found on PATH."
    }
}

$nvccPath = Get-Command nvcc -ErrorAction SilentlyContinue
if (-not $nvccPath) {
    throw 'NVIDIA CUDA Toolkit is required (nvcc not found). Install the CUDA 12 toolkit and add it to PATH.'
}

# Clone or update source
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $sourceDir '.git'))) {
    Write-Host 'Cloning stable-diffusion.cpp...'
    git clone --recursive https://github.com/leejet/stable-diffusion.cpp $sourceDir
    if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }
}

git -C $sourceDir fetch origin
git -C $sourceDir checkout --detach $pinnedRevision
if ($LASTEXITCODE -ne 0) { throw "Could not checkout revision $pinnedRevision." }
git -C $sourceDir submodule update --init --recursive

# Build with CUDA
$cmakeBuildDir = Join-Path $sourceDir 'build'
if (Test-Path -LiteralPath $cmakeBuildDir) {
    Remove-Item -LiteralPath $cmakeBuildDir -Recurse -Force
}

Write-Host 'Configuring CMake with CUDA...'
cmake -S $sourceDir -B $cmakeBuildDir -DSD_CUDA=ON -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }

Write-Host 'Building stable-diffusion.cpp (this may take several minutes)...'
cmake --build $cmakeBuildDir --config Release
if ($LASTEXITCODE -ne 0) { throw 'CMake build failed.' }

# Collect output binaries
New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null
$binDir = Join-Path $cmakeBuildDir 'bin\Release'
if (-not (Test-Path -LiteralPath $binDir)) {
    $binDir = Join-Path $cmakeBuildDir 'bin'
}

$requiredFiles = @('sd-server.exe', 'sd-cli.exe', 'stable-diffusion.dll', 'ggml.dll', 'ggml-base.dll', 'ggml-cuda.dll')
foreach ($file in (Get-ChildItem -Path $binDir -Recurse -File -Include '*.exe','*.dll','*.txt')) {
    Copy-Item -LiteralPath $file.FullName -Destination $runtimeDir -Force
}

# Copy CUDA runtime DLLs that ship with the toolkit
$cudaBinDir = Split-Path (Get-Command nvcc).Source -Parent
foreach ($cudaDll in @('cublas64_12.dll', 'cublasLt64_12.dll', 'cudart64_12.dll')) {
    $source = Join-Path $cudaBinDir $cudaDll
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $runtimeDir -Force
    }
}

# Verify
$serverExe = Join-Path $runtimeDir 'sd-server.exe'
if (-not (Test-Path -LiteralPath $serverExe)) {
    throw "Build completed but sd-server.exe was not found in $runtimeDir."
}

$cudaDll = Join-Path $runtimeDir 'ggml-cuda.dll'
if (-not (Test-Path -LiteralPath $cudaDll)) {
    Write-Warning 'ggml-cuda.dll was not produced. Image generation will not have GPU acceleration.'
}

Write-Host "Installed stable-diffusion.cpp ($pinnedRevision) with CUDA at $runtimeDir" -ForegroundColor Green
