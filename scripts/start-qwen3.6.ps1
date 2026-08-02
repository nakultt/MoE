param(
    [int]$Port = 8081,
    [int]$Context = 8192
)

$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path $PSScriptRoot -Parent) 'settings.ps1')
$server = Join-Path $LlamaRuntimeDir 'llama-server.exe'
$model = $QwenModelPath

if (-not (Test-Path -LiteralPath $server)) {
    throw "llama-server.exe was not found at $server"
}

if (-not (Test-Path -LiteralPath $model)) {
    throw "The Qwen GGUF was not found at $model"
}

$serverArgs = @(
    '-m', $model,
    '-c', "$Context",
    '-ngl', '999',
    '-ncmoe', '30',
    '-ctk', 'f16',
    '-ctv', 'f16',
    '-fa', 'on',
    '-t', '14',
    '-C', '0xFFFF',
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', '127.0.0.1',
    '--port', "$Port",
    '--jinja',
    '-rea', 'off'
)

Write-Host "Starting Qwen3.6-35B-A3B at http://127.0.0.1:$Port"
Write-Host 'Press Ctrl+C to stop the server.'
& $server @serverArgs
exit $LASTEXITCODE
