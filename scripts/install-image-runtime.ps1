param(
    [switch]$Force,

    # Install into another folder (for example to benchmark a newer build) instead of runtime\stable-diffusion.cpp.
    [string]$Destination
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')

# Official leejet/stable-diffusion.cpp Windows CUDA 12 build. To upgrade: change the release, archive and
# SHA256 below (GitHub shows each asset's sha256 digest on the release page), run this script, then
# re-run benchmarks\benchmark-flux.ps1.
$release = 'master-1117-0d71f96'
$archiveName = 'sd-master-0d71f96-bin-win-cuda12-x64.zip'
$archiveHash = 'B2DB431B50E5C5B87C25B6B52006627EAE866193634112F68C57DAF7C1C8CB0D'
$archiveUrl = "https://github.com/leejet/stable-diffusion.cpp/releases/download/$release/$archiveName"

# The sd.cpp archive does not ship the CUDA 12 runtime; take it from llama.cpp's pinned cudart package.
$cudaName = 'cudart-llama-bin-win-cuda-12.4-x64.zip'
$cudaHash = '8C79A9B226DE4B3CACFD1F83D24F962D0773BE79F1E7B75C6AF4DED7E32AE1D6'
$cudaUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$LlamaRuntimeRelease/$cudaName"

$runtimeDir = if ([string]::IsNullOrWhiteSpace($Destination)) {
    Join-Path $repoRoot 'runtime\stable-diffusion.cpp'
} else {
    [IO.Path]::GetFullPath($Destination)
}
$resolvedRepo = (Resolve-Path -LiteralPath $repoRoot).Path
if (-not $runtimeDir.StartsWith($resolvedRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to modify runtime outside the repository: $runtimeDir"
}

$versionMarker = Join-Path $runtimeDir 'moe-runtime-version.txt'
if (Test-Path -LiteralPath $runtimeDir) {
    $installed = if (Test-Path -LiteralPath $versionMarker) { (Get-Content -LiteralPath $versionMarker -Raw).Trim() } else { '' }
    if ($installed -eq $release -and -not $Force) {
        Write-Host "stable-diffusion.cpp $release is already installed at $runtimeDir"
        exit 0
    }
    Write-Host "Replacing image runtime '$installed' with $release..."
    Remove-Item -LiteralPath $runtimeDir -Recurse -Force
}

$downloadDir = Join-Path $repoRoot 'work\downloads'
New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
foreach ($archive in @(
    @{ Name = $archiveName; Url = $archiveUrl; Hash = $archiveHash },
    @{ Name = $cudaName; Url = $cudaUrl; Hash = $cudaHash }
)) {
    $path = Join-Path $downloadDir $archive.Name
    $valid = (Test-Path -LiteralPath $path) -and ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $archive.Hash)
    if (-not $valid) {
        Write-Host "Downloading $($archive.Name)..."
        Invoke-WebRequest -Uri $archive.Url -OutFile $path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $archive.Hash) {
            throw "SHA256 verification failed for $($archive.Name)"
        }
    }
}

New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
Expand-Archive -LiteralPath (Join-Path $downloadDir $archiveName) -DestinationPath $runtimeDir -Force
Expand-Archive -LiteralPath (Join-Path $downloadDir $cudaName) -DestinationPath $runtimeDir -Force

foreach ($file in @('sd-server.exe', 'stable-diffusion.dll', 'ggml-cuda.dll', 'cudart64_12.dll', 'cublas64_12.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeDir $file))) {
        throw "Image runtime installation did not produce $file."
    }
}

Set-Content -LiteralPath $versionMarker -Value $release -Encoding ascii
Write-Host "Installed stable-diffusion.cpp $release with CUDA 12 at $runtimeDir" -ForegroundColor Green
