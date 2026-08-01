param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')

$release = 'b10218'
$baseName = 'llama-b10218-bin-win-cuda-13.3-x64.zip'
$cudaName = 'cudart-llama-bin-win-cuda-13.3-x64.zip'
$baseUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$baseName"
$cudaUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$cudaName"
$expectedHashes = @{
    $baseName = 'C275A6A5923E1D665D0B1D2B32AE266241A83936E9A5C2F0453B99D21877C88F'
    $cudaName = '1462A050EB4C684921BA51DCC4CC488A036674C3E73E9945EE705B854808D03E'
}

$resolvedRepo = (Resolve-Path -LiteralPath $repoRoot).Path
$runtimeParent = Split-Path $LlamaRuntimeDir -Parent
if (-not $LlamaRuntimeDir.StartsWith($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to modify runtime outside the repository: $LlamaRuntimeDir"
}

if (Test-Path -LiteralPath (Join-Path $LlamaRuntimeDir 'llama-server.exe')) {
    if (-not $Force) {
        Write-Host "Runtime is already installed at $LlamaRuntimeDir"
        Write-Host 'Use -Force only if you intentionally want to reinstall it.'
        exit 0
    }
    Remove-Item -LiteralPath $LlamaRuntimeDir -Recurse -Force
}

$downloadDir = Join-Path $repoRoot 'work\downloads'
New-Item -ItemType Directory -Path $downloadDir,$runtimeParent -Force | Out-Null

foreach ($archive in @(
    @{ Name = $baseName; Url = $baseUrl },
    @{ Name = $cudaName; Url = $cudaUrl }
)) {
    $archivePath = Join-Path $downloadDir $archive.Name
    $valid = (Test-Path -LiteralPath $archivePath) -and ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -eq $expectedHashes[$archive.Name])
    if (-not $valid) {
        if (Test-Path -LiteralPath $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }
        Write-Host "Downloading $($archive.Name)..."
        Invoke-WebRequest -Uri $archive.Url -OutFile $archivePath
    }
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHashes[$archive.Name]) {
        throw "SHA256 verification failed for $($archive.Name)"
    }
}

New-Item -ItemType Directory -Path $LlamaRuntimeDir -Force | Out-Null
Expand-Archive -LiteralPath (Join-Path $downloadDir $baseName) -DestinationPath $LlamaRuntimeDir -Force
Expand-Archive -LiteralPath (Join-Path $downloadDir $cudaName) -DestinationPath $LlamaRuntimeDir -Force

if (-not (Test-Path -LiteralPath (Join-Path $LlamaRuntimeDir 'llama-server.exe'))) {
    throw 'Runtime installation did not produce llama-server.exe.'
}
if (-not (Test-Path -LiteralPath (Join-Path $LlamaRuntimeDir 'ggml-cuda.dll'))) {
    throw 'Runtime installation did not produce ggml-cuda.dll.'
}

Write-Host "Installed llama.cpp $release with CUDA 13.3 at $LlamaRuntimeDir"
