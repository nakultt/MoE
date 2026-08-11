[CmdletBinding()]
param(
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$certificatePath = Join-Path $repoRoot 'artifacts\MoE.ModelManager-devcert.cer'
$packagePath = Join-Path $repoRoot 'artifacts\MoE.ModelManager_1.7.1.0_x64.msix'

if (-not (Test-Path -LiteralPath $certificatePath)) {
    throw "Development certificate was not found at $certificatePath. Run scripts\build-installer.ps1 first."
}
if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "MSIX package was not found at $packagePath. Run scripts\build-installer.ps1 first."
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdministrator) {
    $shellPath = (Get-Process -Id $PID).Path
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Elevated"
    $process = Start-Process -FilePath $shellPath -Verb RunAs -ArgumentList $arguments -PassThru -Wait
    exit $process.ExitCode
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The public certificate does not match the MSIX signer.'
}

$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -EQ $certificate.Thumbprint
if ($null -eq $trusted) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

Write-Host 'The development certificate is trusted in Local Computer\Trusted People.'
Write-Host 'Opening App Installer...'
Start-Process -FilePath $packagePath
