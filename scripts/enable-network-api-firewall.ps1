[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ruleName = 'MoE Model Manager LAN API'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $shellPath = (Get-Process -Id $PID).Path
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    $process = Start-Process -FilePath $shellPath -Verb RunAs -ArgumentList $arguments -PassThru -Wait
    exit $process.ExitCode
}

Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 8081 -Action Allow -Profile Private | Out-Null
Write-Host 'Opened TCP port 8081 for MoE Model Manager on Private networks only.'
