<#
.SYNOPSIS
Benchmarks any LLM entry in Configuration\models.json with the same llama-server arguments the app uses.

.DESCRIPTION
Reads the model entry by id, starts llama-server with the arguments LlamaArgumentsBuilder produces,
runs deterministic generations, and prints median tokens per second and GPU memory as JSON.
Any tuning value can be overridden to search for a faster configuration before writing it to models.json.

.EXAMPLE
.\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini
.\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini -CpuMoeLayers 14 -Context 16384
.\benchmarks\benchmark-model.ps1 -ModelId gemma-4-26b-a4b -RuntimeDir E:\Github\MoE\runtime\llama.cpp-b12651-cuda13.5
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ModelId,

    [string]$RuntimeDir,

    [ValidateRange(-1, 200)]
    [int]$CpuMoeLayers = -1,

    [ValidateRange(0, 1048576)]
    [int]$Context = 0,

    [ValidateSet('', 'f16', 'q8_0', 'q4_0')]
    [string]$CacheType = '',

    [ValidateRange(0, 64)]
    [int]$Threads = 0,

    [ValidateRange(-1, 16)]
    [int]$DraftTokens = -1,

    # Try a drafter that is not in the catalog yet (for example a new MTP/DSpark GGUF).
    [string]$DraftModel,

    [ValidateSet('', 'draft-mtp', 'draft-dspark', 'draft-eagle3', 'draft-dflash')]
    [string]$DraftType = '',

    [switch]$NoVision,

    # Extra llama-server arguments to try, appended after the catalog's extraArguments.
    [string[]]$ExtraArgument = @(),

    [ValidateRange(128, 2048)]
    [int]$GenerateTokens = 512,

    [ValidateRange(1, 5)]
    [int]$Runs = 3,

    [ValidateRange(1024, 65535)]
    [int]$Port = 8094
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'settings.ps1')
if ([string]::IsNullOrWhiteSpace($RuntimeDir)) {
    $RuntimeDir = $LlamaRuntimeDir
}
$server = Join-Path $RuntimeDir 'llama-server.exe'
if (-not (Test-Path -LiteralPath $server)) {
    throw "llama-server.exe was not found at $server"
}

$catalogPath = Join-Path $root 'src\MoE.ModelManager\Configuration\models.json'
$model = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json | Where-Object { $_.id -eq $ModelId }
if ($null -eq $model) {
    throw "Model '$ModelId' is not in $catalogPath"
}
if (-not (Test-Path -LiteralPath $model.modelPath)) {
    throw "The GGUF was not found at $($model.modelPath)"
}

$ncmoe = if ($CpuMoeLayers -ge 0) { $CpuMoeLayers } else { [int]$model.cpuMoeLayers }
$ctx = if ($Context -gt 0) { $Context } else { [int]$model.contextSize }
$cache = if ($CacheType) { $CacheType } else { $model.cacheType }
$threadCount = if ($Threads -gt 0) { $Threads } else { [int]$model.threads }
$specType = if ($DraftType) { $DraftType } elseif ($model.draftType) { $model.draftType } else { 'draft-mtp' }
$specModel = if ($DraftModel) { $DraftModel } else { $model.draftModelPath }
$draftCount = if ($DraftTokens -ge 0) { $DraftTokens } else { [int]$model.draftTokens }

$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$tag = "$ModelId-ncmoe-$ncmoe-ctx-$ctx-$cache-t-$threadCount-draft-$draftCount"
$stdoutLog = Join-Path $logDir "$tag.out.log"
$stderrLog = Join-Path $logDir "$tag.err.log"

# Keep in sync with src\MoE.ModelManager\Services\LlamaArgumentsBuilder.cs.
$serverArgs = @(
    '-m', $model.modelPath,
    '-c', "$ctx",
    '-ngl', '999',
    '-ncmoe', "$ncmoe",
    '-ctk', $cache,
    '-ctv', $cache,
    '-fa', 'on',
    '-t', "$threadCount",
    '-C', '0xFFFF',
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', '127.0.0.1',
    '--port', "$Port",
    '--jinja',
    '-rea', 'off'
)
if ($specModel -and $draftCount -gt 0) {
    $serverArgs += @(
        '--spec-type', $specType,
        '--spec-draft-model', $specModel,
        '--spec-draft-n-max', "$draftCount",
        '--spec-draft-ngl', '999'
    )
}
if ($model.visionProjectorPath -and -not $NoVision) {
    $serverArgs += @('--mmproj', $model.visionProjectorPath, '--no-mmproj-offload')
}
if ($model.extraArguments) {
    $serverArgs += @($model.extraArguments)
}
$serverArgs += @($ExtraArgument)

$process = $null
try {
    $process = Start-Process -FilePath $server -ArgumentList $serverArgs -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

    $ready = $false
    $deadline = (Get-Date).AddMinutes(8)
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
    $serverMemoryMiB = [math]::Round((Get-Process -Id $process.Id).WorkingSet64 / 1MB)
    $samples = @()
    $lastResponse = $null
    for ($run = 1; $run -le $Runs; $run++) {
        $payload = @{
            prompt = 'Write a detailed technical guide of at least 800 words explaining how mixture-of-experts language models route tokens, use memory, and achieve fast local inference. Include concrete examples and practical tradeoffs.'
            n_predict = $GenerateTokens
            temperature = 0
            seed = 42
            cache_prompt = $false
        } | ConvertTo-Json
        $lastResponse = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/completion" -ContentType 'application/json' -Body $payload -TimeoutSec 900
        $samples += [double]$lastResponse.timings.predicted_per_second
    }

    $ordered = @($samples | Sort-Object)
    $middle = [int][math]::Floor($ordered.Count / 2)
    $median = if ($ordered.Count % 2 -eq 0) { ($ordered[$middle - 1] + $ordered[$middle]) / 2 } else { $ordered[$middle] }

    [pscustomobject]@{
        model = $ModelId
        runtime = Split-Path $RuntimeDir -Leaf
        cpu_moe_layers = $ncmoe
        context = $ctx
        cache_type = $cache
        threads = $threadCount
        draft_tokens = $draftCount
        samples_tps = @($samples | ForEach-Object { [math]::Round($_, 2) })
        median_tps = [math]::Round($median, 2)
        prompt_tps = [math]::Round([double]$lastResponse.timings.prompt_per_second, 2)
        gpu_memory_mib = $gpuMemoryMiB
        server_ram_mib = $serverMemoryMiB
        sample_text = ($lastResponse.content.Substring(0, [math]::Min(160, $lastResponse.content.Length)))
    } | ConvertTo-Json
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
}
