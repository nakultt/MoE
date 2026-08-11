[CmdletBinding()]
param(
    [int]$Port = 8081,
    [int]$Context = 8192,
    [string]$HostAddress = '127.0.0.1'
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'settings.ps1')

$server = Join-Path $LlamaRuntimeDir 'llama-server.exe'
foreach ($requiredPath in @($server, $NemotronOmniModelPath, $NemotronOmniProjectorPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required Nemotron Omni file was not found: $requiredPath"
    }
}

$serverArgs = @(
    '-m', $NemotronOmniModelPath,
    '-c', "$Context",
    '-ngl', '999',
    '-ncmoe', '40',
    '-ctk', 'q8_0',
    '-ctv', 'q8_0',
    '-fa', 'on',
    '-t', '16',
    '-C', '0xFFFF',
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', $HostAddress,
    '--port', "$Port",
    '--alias', 'nemotron-3-nano-omni-30b-a3b',
    '--jinja',
    '-rea', 'off',
    '--metrics',
    '--mmproj', $NemotronOmniProjectorPath,
    '--no-mmproj-offload'
)

Write-Host "Starting Nemotron 3 Nano Omni at http://${HostAddress}:$Port"
Write-Host 'Reasoning is selected per request with chat_template_kwargs.enable_thinking.'
Write-Host 'Press Ctrl+C to stop the server.'
& $server @serverArgs
exit $LASTEXITCODE
