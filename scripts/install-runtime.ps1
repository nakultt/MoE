param(
    [switch]$Force,

    # Install into another folder (for example to benchmark a newer build) instead of $LlamaRuntimeDir.
    [string]$Destination,

    [ValidateSet('12.4', '13.5')]
    [string]$Cuda = $LlamaRuntimeCuda
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')
if ([string]::IsNullOrWhiteSpace($Cuda)) {
    $Cuda = $LlamaRuntimeCuda
}
if (-not [string]::IsNullOrWhiteSpace($Destination)) {
    $LlamaRuntimeDir = [IO.Path]::GetFullPath($Destination)
}

# To upgrade llama.cpp: change $LlamaRuntimeRelease in settings.ps1 and the hashes below
# (GitHub shows each asset's sha256 digest on the release page).
$release = $LlamaRuntimeRelease
$baseName = "llama-$release-bin-win-cuda-$Cuda-x64.zip"
$cudaName = "cudart-llama-bin-win-cuda-$Cuda-x64.zip"
$baseUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$baseName"
$cudaUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$release/$cudaName"
$expectedHashes = @{
    'llama-b12651-bin-win-cuda-12.4-x64.zip' = '1A6C090F76FAB41260B5B7597C6FB1ADD1FAD2EFAAC692AE2673A22366220A29'
    'cudart-llama-bin-win-cuda-12.4-x64.zip' = '8C79A9B226DE4B3CACFD1F83D24F962D0773BE79F1E7B75C6AF4DED7E32AE1D6'
    'llama-b12651-bin-win-cuda-13.5-x64.zip' = '5AD87E0771F3A129F6461B5FC706D8C2C5E5B71B2BD1F953A4221CA4D7797FEF'
    'cudart-llama-bin-win-cuda-13.5-x64.zip' = 'C2BF4CC202BFBAAA7A0B75F5082457C3811326947BB216059B72839D844E9CBA'
}
foreach ($name in @($baseName, $cudaName)) {
    if (-not $expectedHashes.ContainsKey($name)) {
        throw "No pinned SHA256 for $name. Add it to scripts\install-runtime.ps1."
    }
}

$resolvedRepo = (Resolve-Path -LiteralPath $repoRoot).Path
$runtimeParent = Split-Path $LlamaRuntimeDir -Parent
if (-not $LlamaRuntimeDir.StartsWith($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to modify runtime outside the repository: $LlamaRuntimeDir"
}

$versionMarker = Join-Path $LlamaRuntimeDir 'moe-runtime-version.txt'
$wantedVersion = "$release-cuda$Cuda"
if (Test-Path -LiteralPath $LlamaRuntimeDir) {
    $installedVersion = if (Test-Path -LiteralPath $versionMarker) { (Get-Content -LiteralPath $versionMarker -Raw).Trim() } else { '' }
    if ($installedVersion -eq $wantedVersion -and -not $Force) {
        Write-Host "llama.cpp $wantedVersion is already installed at $LlamaRuntimeDir"
        exit 0
    }
    Write-Host "Replacing runtime '$installedVersion' with $wantedVersion..."
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

Set-Content -LiteralPath $versionMarker -Value $wantedVersion -Encoding ascii
Write-Host "Installed llama.cpp $release with CUDA $Cuda at $LlamaRuntimeDir"
