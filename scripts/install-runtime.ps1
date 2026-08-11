param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')

$release = 'b10359'
$baseName = 'llama-b10359-bin-win-cuda-12.4-x64.zip'
$cudaName = 'cudart-llama-bin-win-cuda-12.4-x64.zip'
$baseUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$baseName"
$cudaUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$cudaName"
$expectedHashes = @{
    $baseName = '73365C6A5FADACCB25502CF054CF5E692C7E4D58CA9E2E228415223470E7D06E'
    $cudaName = '8C79A9B226DE4B3CACFD1F83D24F962D0773BE79F1E7B75C6AF4DED7E32AE1D6'
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

Write-Host "Installed llama.cpp $release with CUDA 12.4 at $LlamaRuntimeDir"
