<#
.SYNOPSIS
Benchmarks FLUX.2 Klein image generation with the same sd-server arguments the app uses.

.EXAMPLE
.\benchmarks\benchmark-flux.ps1
.\benchmarks\benchmark-flux.ps1 -RuntimeDir runtime\stable-diffusion.cpp-test -Size 768x768
#>
param(
    # Image model id from image-models.json; defaults to the first (default) entry.
    [string]$ModelId,

    [string]$RuntimeDir,

    [ValidateSet('512x512', '768x768', '1024x1024')]
    [string]$Size = '512x512',

    [ValidateRange(1, 5)]
    [int]$Runs = 3,

    [ValidateRange(1024, 65535)]
    [int]$Port = 8095,

    # Attention / convolution flags to test; defaults match the app. The model's extraArguments are appended.
    [string[]]$PerfArgs = @('--flash-attn', 'off,diffusion=on', '--conv-direct', 'auto,vae=on')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($RuntimeDir)) {
    $RuntimeDir = Join-Path $root 'runtime\stable-diffusion.cpp'
}
$server = Join-Path (Resolve-Path $RuntimeDir) 'sd-server.exe'
$catalog = @(Get-Content -LiteralPath (Join-Path $root 'src\MoE.ModelManager\Configuration\image-models.json') -Raw | ConvertFrom-Json)
$model = if ($ModelId) { $catalog | Where-Object { $_.id -eq $ModelId } } else { $catalog[0] }
if ($null -eq $model) {
    throw "Image model '$ModelId' is not in image-models.json"
}

$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$stdoutLog = Join-Path $logDir 'flux.out.log'
$stderrLog = Join-Path $logDir 'flux.err.log'

# Keep in sync with src\MoE.ModelManager\Services\ImageRuntimeArgumentsBuilder.cs.
$serverArgs = @(
    '--listen-ip', '127.0.0.1',
    '--listen-port', "$Port",
    '--diffusion-model', $model.diffusionModelPath,
    '--llm', $model.textEncoderPath,
    '--vae', $model.vaePath,
    '--backend', 'diffusion=CUDA0,te=CUDA0,vae=CUDA0',
    '--params-backend', 'diffusion=CUDA0,te=CUDA0,vae=CUDA0',
    '--threads', "$($model.threads)",
    '--steps', "$($model.steps)",
    '--cfg-scale', '1.0',
    '--seed', '-1',
    '--mmap'
) + $PerfArgs + @($model.extraArguments | Where-Object { $_ })

$process = $null
try {
    $loadTimer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $server -ArgumentList $serverArgs -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog
    $ready = $false
    $deadline = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            $tail = Get-Content -LiteralPath $stdoutLog, $stderrLog -Tail 40 -ErrorAction SilentlyContinue
            throw "sd-server exited during startup with code $($process.ExitCode).`n$($tail -join "`n")"
        }
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:$Port/v1/models" -TimeoutSec 2 | Out-Null
            $ready = $true
            break
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $ready) {
        throw 'Timed out waiting for sd-server.'
    }
    $loadTimer.Stop()

    $seconds = @()
    for ($run = 0; $run -le $Runs; $run++) {
        $body = @{
            prompt = 'A premium product photograph of a futuristic silver wristwatch on black stone, dramatic studio lighting, crisp reflections, high detail'
            n = 1
            size = $Size
            output_format = 'png'
        } | ConvertTo-Json
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/v1/images/generations" -ContentType 'application/json' -Body $body -TimeoutSec 900
        $timer.Stop()
        if ($run -eq 0) {
            [IO.File]::WriteAllBytes((Join-Path $logDir "flux-$Size.png"), [Convert]::FromBase64String($response.data[0].b64_json))
            continue
        }
        $seconds += $timer.Elapsed.TotalSeconds
    }
    $gpuMemoryMiB = [int]((& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits | Select-Object -First 1).Trim())
    $ordered = @($seconds | Sort-Object)

    [pscustomobject]@{
        runtime = Split-Path $RuntimeDir -Leaf
        model = $model.id
        perf_args = ($PerfArgs -join ' ')
        size = $Size
        load_seconds = [math]::Round($loadTimer.Elapsed.TotalSeconds, 2)
        warm_samples_seconds = @($seconds | ForEach-Object { [math]::Round($_, 2) })
        median_seconds = [math]::Round($ordered[[int][math]::Floor($ordered.Count / 2)], 2)
        gpu_memory_mib = $gpuMemoryMiB
    } | ConvertTo-Json
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
}
