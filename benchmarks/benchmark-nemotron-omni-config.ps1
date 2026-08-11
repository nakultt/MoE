[CmdletBinding()]
param(
    [ValidateRange(0, 52)]
    [int]$CpuMoeLayers = 52,

    [ValidateRange(2048, 16384)]
    [int]$Context = 8192,

    [ValidateSet('f16', 'q8_0', 'q4_0')]
    [string]$CacheType = 'q8_0',

    [ValidateRange(1, 28)]
    [int]$Threads = 16,

    [ValidatePattern('^0x[0-9A-Fa-f]+$')]
    [string]$CpuMask = '0xFFFF',

    [ValidateRange(64, 1024)]
    [int]$GenerateTokens = 256,

    [ValidateRange(1, 5)]
    [int]$Runs = 2,

    [switch]$NoMmap,

    [ValidateRange(1024, 65535)]
    [int]$Port = 8094
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'settings.ps1')
$server = Join-Path $LlamaRuntimeDir 'llama-server.exe'
$logDirectory = Join-Path $root 'artifacts\nemotron-benchmark'
$tag = "ncmoe-$CpuMoeLayers-c$Context-t$Threads"
$stdoutLog = Join-Path $logDirectory "$tag.stdout.log"
$stderrLog = Join-Path $logDirectory "$tag.stderr.log"

foreach ($requiredPath in @($server, $NemotronOmniModelPath, $NemotronOmniProjectorPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required benchmark file was not found: $requiredPath"
    }
}
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$arguments = @(
    '-m', $NemotronOmniModelPath,
    '-c', "$Context",
    '-ngl', '999',
    '-ncmoe', "$CpuMoeLayers",
    '-ctk', $CacheType,
    '-ctv', $CacheType,
    '-fa', 'on',
    '-t', "$Threads",
    '-C', $CpuMask,
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', '127.0.0.1',
    '--port', "$Port",
    '--jinja',
    '-rea', 'off',
    '--mmproj', $NemotronOmniProjectorPath,
    '--no-mmproj-offload'
)
if ($NoMmap) {
    $arguments += '--no-mmap'
}

$process = Start-Process `
    -FilePath $server `
    -ArgumentList $arguments `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog
try {
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddMinutes(6)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            $detail = Get-Content -LiteralPath $stderrLog -Tail 100 | Out-String
            throw "llama-server exited with code $($process.ExitCode).`n$detail"
        }
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2
            if ($health.status -eq 'ok') {
                $ready = $true
                break
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 750
    }
    if (-not $ready) {
        throw 'Timed out waiting for the Nemotron benchmark runtime.'
    }

    $dedicatedMemoryMiB = [int]((& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits | Select-Object -First 1).Trim())
    $samples = @()
    for ($run = 1; $run -le $Runs; $run++) {
        $payload = @{
            prompt = 'Write a detailed technical explanation of mixture-of-experts inference, GPU offload, memory bandwidth, and practical performance tuning. Continue until the token limit.'
            n_predict = $GenerateTokens
            temperature = 0
            seed = 42
            cache_prompt = $false
        } | ConvertTo-Json -Compress
        $response = Invoke-RestMethod `
            -Method Post `
            -Uri "http://127.0.0.1:$Port/completion" `
            -ContentType 'application/json' `
            -Body $payload `
            -TimeoutSec 600
        $samples += [double]$response.timings.predicted_per_second
    }

    $ordered = @($samples | Sort-Object)
    $middle = [int][math]::Floor($ordered.Count / 2)
    $median = if ($ordered.Count % 2 -eq 0) {
        ($ordered[$middle - 1] + $ordered[$middle]) / 2
    }
    else {
        $ordered[$middle]
    }
    [pscustomobject]@{
        cpu_moe_layers = $CpuMoeLayers
        context = $Context
        cache_type = $CacheType
        threads = $Threads
        cpu_mask = $CpuMask
        no_mmap = [bool]$NoMmap
        samples_tps = @($samples | ForEach-Object { [math]::Round($_, 2) })
        median_tps = [math]::Round($median, 2)
        dedicated_gpu_memory_mib = $dedicatedMemoryMiB
    } | ConvertTo-Json
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}
