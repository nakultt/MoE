[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('qwen3.6-35b-a3b', 'gemma-4-26b-a4b')]
    [string]$ModelId,

    [Parameter(Mandatory)]
    [string]$ImagePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$serverPath = Join-Path $repoRoot 'runtime\llama.cpp-b10218-cuda13\llama-server.exe'
$catalogPath = Join-Path $repoRoot 'src\MoE.ModelManager\Configuration\models.json'
$logDirectory = Join-Path $repoRoot 'artifacts\vision-test'
$resolvedImagePath = (Resolve-Path -LiteralPath $ImagePath).Path
$models = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$model = $models | Where-Object id -EQ $ModelId | Select-Object -First 1
if ($null -eq $model) {
    throw "Model $ModelId was not found in the application catalog."
}

foreach ($requiredPath in @($serverPath, $model.modelPath, $model.visionProjectorPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required vision runtime file was not found: $requiredPath"
    }
}

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$stdoutPath = Join-Path $logDirectory "$ModelId.stdout.log"
$stderrPath = Join-Path $logDirectory "$ModelId.stderr.log"
$arguments = @(
    '-m', $model.modelPath,
    '-c', $model.contextSize,
    '-ngl', '999',
    '-ncmoe', $model.cpuMoeLayers,
    '-ctk', $model.cacheType,
    '-ctv', $model.cacheType,
    '-fa', 'on',
    '-t', $model.threads,
    '-C', '0xFFFF',
    '--cpu-strict', '1',
    '--prio', '2',
    '-np', '1',
    '--host', '127.0.0.1',
    '--port', '8092',
    '--alias', $model.id,
    '--jinja',
    '-rea', 'off',
    '--metrics',
    '--mmproj', $model.visionProjectorPath,
    '--no-mmproj-offload'
)
if (-not [string]::IsNullOrWhiteSpace($model.draftModelPath) -and $model.draftTokens -gt 0) {
    $arguments += @(
        '--spec-type', 'draft-mtp',
        '--spec-draft-model', $model.draftModelPath,
        '--spec-draft-n-max', $model.draftTokens,
        '--spec-draft-ngl', '999'
    )
}
if ($model.visionImageTokens -gt 0) {
    $arguments += @(
        '--image-min-tokens', $model.visionImageTokens,
        '--image-max-tokens', $model.visionImageTokens
    )
}

$process = Start-Process `
    -FilePath $serverPath `
    -ArgumentList $arguments `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -WindowStyle Hidden `
    -PassThru
try {
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddMinutes(6)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            $detail = Get-Content -LiteralPath $stderrPath -Tail 50 | Out-String
            throw "Vision runtime exited with code $($process.ExitCode).`n$detail"
        }

        try {
            $health = Invoke-RestMethod -Uri 'http://127.0.0.1:8092/health' -TimeoutSec 2
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
        throw "Timed out waiting for $ModelId vision runtime."
    }

    $extension = [IO.Path]::GetExtension($resolvedImagePath).ToLowerInvariant()
    $mediaType = switch ($extension) {
        '.png' { 'image/png' }
        '.jpg' { 'image/jpeg' }
        '.jpeg' { 'image/jpeg' }
        '.webp' { 'image/webp' }
        default { throw "Unsupported test image extension: $extension" }
    }
    $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($resolvedImagePath))
    $payload = @{
        model = $model.id
        messages = @(
            @{
                role = 'user'
                content = @(
                    @{
                        type = 'text'
                        text = 'What application is shown in this screenshot? Answer in one concise sentence.'
                    }
                    @{
                        type = 'image_url'
                        image_url = @{ url = "data:$mediaType;base64,$base64" }
                    }
                )
            }
        )
        stream = $false
        temperature = 0.1
        max_tokens = 80
    } | ConvertTo-Json -Depth 8 -Compress
    $response = Invoke-RestMethod `
        -Uri 'http://127.0.0.1:8092/v1/chat/completions' `
        -Method Post `
        -ContentType 'application/json' `
        -Body $payload `
        -TimeoutSec 300
    [pscustomobject]@{
        Model = $ModelId
        Answer = $response.choices[0].message.content
        PromptTokens = $response.usage.prompt_tokens
        CompletionTokens = $response.usage.completion_tokens
        PredictedTokensPerSecond = $response.timings.predicted_per_second
    }
}
finally {
    if (-not $process.HasExited) {
        $resolvedProcessPath = $process.MainModule.FileName
        if (-not $resolvedProcessPath.Equals($serverPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to stop unexpected process $resolvedProcessPath"
        }

        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}
