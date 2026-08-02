param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 30)]
    [int]$CpuMoeLayers,

    [ValidateSet('none', 'q4', 'q8')]
    [string]$Draft = 'none',

    [ValidateRange(1, 8)]
    [int]$DraftTokens = 3,

    [ValidateRange(2048, 32768)]
    [int]$Context = 8192,

    [ValidateRange(128, 2048)]
    [int]$GenerateTokens = 512,

    [ValidateRange(1, 5)]
    [int]$Runs = 3,

    [ValidateRange(1024, 65535)]
    [int]$Port = 8093,

    [switch]$UltimatePerformance
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'settings.ps1')
$server = Join-Path $LlamaRuntimeDir 'llama-server.exe'
$model = $GemmaTargetModelPath
$draftModels = @{
    q4 = $GemmaMtpQ4ModelPath
    q8 = $GemmaMtpQ8ModelPath
}
$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if (-not (Test-Path -LiteralPath $server)) {
    throw "llama-server.exe was not found at $server"
}
if (-not (Test-Path -LiteralPath $model)) {
    throw "The Gemma target model was not found at $model"
}
if ($Draft -ne 'none' -and -not (Test-Path -LiteralPath $draftModels[$Draft])) {
    throw "The $Draft MTP drafter was not found at $($draftModels[$Draft])"
}

$tag = "ncmoe-$CpuMoeLayers-draft-$Draft-n-$DraftTokens"
$stdoutLog = Join-Path $logDir "$tag.out.log"
$stderrLog = Join-Path $logDir "$tag.err.log"

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
    '--jinja',
    '-rea', 'off'
)

if ($Draft -ne 'none') {
    $serverArgs += @(
        '--spec-type', 'draft-mtp',
        '--spec-draft-model', $draftModels[$Draft],
        '--spec-draft-n-max', "$DraftTokens",
        '--spec-draft-ngl', '999'
    )
}

$process = $null
$originalPowerGuid = $null
try {
    if ($UltimatePerformance) {
        $activeScheme = powercfg /getactivescheme
        $guidMatch = [regex]::Match(($activeScheme -join ' '), '[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}')
        if (-not $guidMatch.Success) {
            throw 'Could not determine the active Windows power plan.'
        }
        $originalPowerGuid = $guidMatch.Value
        powercfg /setactive '3a4bbc86-cef3-4dba-b43d-5f015ae0ebc0' | Out-Null
    }

    $process = Start-Process -FilePath $server -ArgumentList $serverArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

    $ready = $false
    $deadline = (Get-Date).AddMinutes(6)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            $tail = Get-Content -LiteralPath $stderrLog -Tail 60 -ErrorAction SilentlyContinue
            throw "llama-server exited during startup with code $($process.ExitCode).`n$($tail -join "`n")"
        }
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2
            if ($health.status -eq 'ok') {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $ready) {
        throw 'Timed out waiting for llama-server to load the model.'
    }

    $gpuMemoryMiB = [int]((& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits | Select-Object -First 1).Trim())
    $samples = @()
    $wallSeconds = @()
    $lastResponse = $null
    for ($run = 1; $run -le $Runs; $run++) {
        $payload = @{
            prompt = 'Write a detailed technical guide of at least 800 words explaining how mixture-of-experts language models route tokens, use memory, and achieve fast local inference. Include concrete examples and practical tradeoffs.'
            n_predict = $GenerateTokens
            temperature = 0
            seed = 42
            cache_prompt = $false
        } | ConvertTo-Json

        $wall = [System.Diagnostics.Stopwatch]::StartNew()
        $lastResponse = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/completion" -ContentType 'application/json' -Body $payload -TimeoutSec 600
        $wall.Stop()
        $samples += [double]$lastResponse.timings.predicted_per_second
        $wallSeconds += $wall.Elapsed.TotalSeconds
    }

    $orderedSamples = @($samples | Sort-Object)
    $middle = [int][math]::Floor($orderedSamples.Count / 2)
    $median = if ($orderedSamples.Count % 2 -eq 0) {
        ($orderedSamples[$middle - 1] + $orderedSamples[$middle]) / 2
    } else {
        $orderedSamples[$middle]
    }

    [pscustomobject]@{
        cpu_moe_layers = $CpuMoeLayers
        draft = $Draft
        draft_tokens = $(if ($Draft -eq 'none') { 0 } else { $DraftTokens })
        context = $Context
        generated_tokens_per_run = $GenerateTokens
        runs = $Runs
        samples_tps = @($samples | ForEach-Object { [math]::Round($_, 2) })
        median_tps = [math]::Round($median, 2)
        average_tps = [math]::Round(($samples | Measure-Object -Average).Average, 2)
        minimum_tps = [math]::Round(($samples | Measure-Object -Minimum).Minimum, 2)
        maximum_tps = [math]::Round(($samples | Measure-Object -Maximum).Maximum, 2)
        prompt_tokens = $lastResponse.tokens_evaluated
        prompt_tps = [math]::Round([double]$lastResponse.timings.prompt_per_second, 2)
        average_wall_seconds = [math]::Round(($wallSeconds | Measure-Object -Average).Average, 2)
        gpu_memory_mib = $gpuMemoryMiB
        stopped_eos = $lastResponse.stopped_eos
        stopped_limit = $lastResponse.stopped_limit
    } | ConvertTo-Json
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
    if ($null -ne $originalPowerGuid) {
        powercfg /setactive $originalPowerGuid | Out-Null
    }
}
