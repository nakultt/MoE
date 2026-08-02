param(
    [switch]$RegenerateCertificate
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'src\MoE.ModelManager\MoE.ModelManager.csproj'
$manifestPath = Join-Path $repoRoot 'src\MoE.ModelManager\Package.appxmanifest'
$artifactDirectory = Join-Path $repoRoot 'artifacts'
$publishDirectory = Join-Path $artifactDirectory 'publish'
$packagePath = Join-Path $artifactDirectory 'MoE.ModelManager_1.3.1.0_x64.msix'
$certificatePath = Join-Path $repoRoot 'devcert.pfx'
$publicCertificatePath = Join-Path $repoRoot 'devcert.cer'
$winAppPath = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools.winapp\0.5.0\tools\win-x64\winapp.exe'

if (-not (Test-Path -LiteralPath $winAppPath)) {
    dotnet restore $projectPath
}
if (-not (Test-Path -LiteralPath $winAppPath)) {
    throw "winapp.exe was not restored at $winAppPath"
}

New-Item -ItemType Directory -Force -Path $artifactDirectory,$publishDirectory | Out-Null
dotnet publish $projectPath -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publishDirectory

if ($RegenerateCertificate -or -not (Test-Path -LiteralPath $certificatePath)) {
    & $winAppPath cert generate `
        --manifest $manifestPath `
        --output $certificatePath `
        --export-cer `
        --valid-days 1095 `
        --if-exists Overwrite
}

$resolvedArtifactDirectory = (Resolve-Path -LiteralPath $artifactDirectory).Path
if (Test-Path -LiteralPath $packagePath) {
    $resolvedPackage = (Resolve-Path -LiteralPath $packagePath).Path
    if (-not $resolvedPackage.StartsWith($resolvedArtifactDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace an installer outside $resolvedArtifactDirectory"
    }

    Remove-Item -LiteralPath $resolvedPackage -Force
}

& $winAppPath package $publishDirectory `
    --manifest $manifestPath `
    --exe 'MoE.ModelManager.exe' `
    --output $packagePath `
    --cert $certificatePath `
    --cert-password 'password'

Copy-Item -LiteralPath $publicCertificatePath -Destination (Join-Path $artifactDirectory 'MoE.ModelManager-devcert.cer') -Force

$hash = Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
Write-Host "Installer: $packagePath"
Write-Host "SHA256: $($hash.Hash)"
Write-Host 'Install with: .\scripts\install-app.ps1'
