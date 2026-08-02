[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 8081,

    [ValidateRange(2048, 32768)]
    [int]$Context = 8192,

    [ValidateRange(0, 30)]
    [int]$CpuMoeLayers = 20,

    [ValidateRange(1, 8)]
    [int]$DraftTokens = 2
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')

$server = Join-Path $LlamaRuntimeDir 'llama-server.exe'
$model = $GemmaTargetModelPath
$draftModel = $GemmaMtpQ8ModelPath
$projector = $GemmaVisionProjectorPath

$requiredFiles = @(
    @{ Name = 'llama-server.exe'; Path = $server },
    @{ Name = 'Gemma target GGUF'; Path = $model },
    @{ Name = 'Gemma Q8 MTP GGUF'; Path = $draftModel },
    @{ Name = 'Gemma vision projector'; Path = $projector }
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile.Path)) {
        throw "$($requiredFile.Name) was not found at $($requiredFile.Path)"
    }
}

$serverArgs = @(
    '-m', $model,
    '-c', "$Context",
    '-ngl', '999',
    '-ncmoe', "$CpuMoeLayers",
    '-ctk', 'q8_0',
    '-ctv', 'q8_0',
    '-fa', 'on',
    '-t', '16',
    '-C', '0xFFFF',
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', '127.0.0.1',
    '--port', "$Port",
    '--alias', 'gemma-4-26b-a4b',
    '--jinja',
    '-rea', 'off',
    '--metrics',
    '--ui',
    '--spec-type', 'draft-mtp',
    '--spec-draft-model', $draftModel,
    '--spec-draft-n-max', "$DraftTokens",
    '--spec-draft-ngl', '999',
    '--mmproj', $projector,
    '--no-mmproj-offload'
)

Write-Host "Starting Gemma 4 26B A4B with Q8 MTP acceleration."
Write-Host "llama.cpp Web UI: http://127.0.0.1:$Port"
Write-Host "OpenAI-compatible API: http://127.0.0.1:$Port/v1"
Write-Host "Configuration: context=$Context, CPU MoE layers=$CpuMoeLayers, MTP draft tokens=$DraftTokens"
Write-Host 'Press Ctrl+C to stop the server.'

& $server @serverArgs
exit $LASTEXITCODE
