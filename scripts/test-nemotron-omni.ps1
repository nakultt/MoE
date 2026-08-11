[CmdletBinding()]
param(
    [string]$ImagePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\MoE.ModelManager\Assets\Square150x150Logo.scale-200.png'),
    [string]$AudioPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'work\api-speech.wav'),
    [string]$VideoPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\FastWan2.2-verified-6c3fdccd01bb4a1fbb8d449c2cd04360.mp4'),
    [int]$Port = 8092
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $repoRoot 'settings.ps1')
$serverPath = Join-Path $LlamaRuntimeDir 'llama-server.exe'
$logDirectory = Join-Path $repoRoot 'artifacts\nemotron-omni-test'
$stdoutPath = Join-Path $logDirectory 'server.stdout.log'
$stderrPath = Join-Path $logDirectory 'server.stderr.log'
$endpoint = "http://127.0.0.1:$Port"

foreach ($requiredPath in @(
    $serverPath,
    $NemotronOmniModelPath,
    $NemotronOmniProjectorPath,
    $ImagePath,
    $AudioPath,
    $VideoPath
)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required Nemotron test file was not found: $requiredPath"
    }
}
foreach ($tool in @('ffmpeg', 'ffprobe')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is required for Nemotron video input and was not found on PATH."
    }
}

function ConvertTo-MediaDataUrl {
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$RawBase64
    )

    $mediaType = switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.png' { 'image/png' }
        '.jpg' { 'image/jpeg' }
        '.jpeg' { 'image/jpeg' }
        '.webp' { 'image/webp' }
        '.wav' { 'audio/wav' }
        '.mp3' { 'audio/mpeg' }
        '.flac' { 'audio/flac' }
        '.mp4' { 'video/mp4' }
        '.mkv' { 'video/x-matroska' }
        '.webm' { 'video/webm' }
        default { throw "Unsupported test media: $Path" }
    }
    $data = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path))
    if ($RawBase64) { $data } else { "data:$mediaType;base64,$data" }
}

function Invoke-NemotronChat {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][object]$Content,
        [Parameter(Mandatory)][bool]$EnableThinking,
        [int]$MaxTokens = 128
    )

    $body = @{
        model = 'nemotron-3-nano-omni-30b-a3b'
        messages = @(@{ role = 'user'; content = $Content })
        chat_template_kwargs = @{ enable_thinking = $EnableThinking }
        temperature = $(if ($EnableThinking) { 0.6 } else { 0.2 })
        max_tokens = $MaxTokens
        stream = $false
    } | ConvertTo-Json -Depth 10 -Compress
    $response = Invoke-RestMethod `
        -Uri "$endpoint/v1/chat/completions" `
        -Method Post `
        -ContentType 'application/json' `
        -Body $body `
        -TimeoutSec 900
    [pscustomobject]@{
        Test = $Name
        Reasoning = $EnableThinking
        Answer = $response.choices[0].message.content
        PromptTokens = $response.usage.prompt_tokens
        CompletionTokens = $response.usage.completion_tokens
        TokensPerSecond = $response.timings.predicted_per_second
    }
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$arguments = @(
    '-m', $NemotronOmniModelPath,
    '-c', '8192',
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
    '--host', '127.0.0.1',
    '--port', "$Port",
    '--alias', 'nemotron-3-nano-omni-30b-a3b',
    '--jinja',
    '-rea', 'off',
    '--metrics',
    '--mmproj', $NemotronOmniProjectorPath,
    '--no-mmproj-offload'
)
$process = Start-Process `
    -FilePath $serverPath `
    -ArgumentList $arguments `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -WindowStyle Hidden `
    -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    do {
        if ($process.HasExited) {
            $detail = Get-Content -LiteralPath $stderrPath -Tail 100 | Out-String
            throw "Nemotron runtime exited with code $($process.ExitCode).`n$detail"
        }
        try {
            $health = Invoke-RestMethod -Uri "$endpoint/health" -TimeoutSec 2
        }
        catch {
            $health = $null
        }
        if ($health.status -eq 'ok') {
            break
        }
        Start-Sleep -Milliseconds 750
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($health.status -ne 'ok') {
        throw 'Timed out waiting for the Nemotron runtime to load.'
    }

    $results = @()
    $results += Invoke-NemotronChat -Name 'text-direct' -Content 'Answer with only the result: 17 multiplied by 19.' -EnableThinking $false -MaxTokens 32
    $results += Invoke-NemotronChat -Name 'text-reasoning' -Content 'Solve 17 multiplied by 19 and briefly verify the arithmetic.' -EnableThinking $true -MaxTokens 128
    $results += Invoke-NemotronChat -Name 'image' -Content @(
        @{ type = 'text'; text = 'Describe this application icon in one sentence.' },
        @{ type = 'image_url'; image_url = @{ url = ConvertTo-MediaDataUrl $ImagePath } }
    ) -EnableThinking $false
    $results += Invoke-NemotronChat -Name 'audio' -Content @(
        @{ type = 'text'; text = 'Transcribe this audio, then summarize it in one sentence.' },
        @{ type = 'input_audio'; input_audio = @{ data = ConvertTo-MediaDataUrl $AudioPath -RawBase64 } }
    ) -EnableThinking $false
    $results += Invoke-NemotronChat -Name 'video' -Content @(
        @{ type = 'text'; text = 'Describe the main visible action in this video in one sentence.' },
        @{ type = 'input_video'; input_video = @{ data = ConvertTo-MediaDataUrl $VideoPath -RawBase64 } }
    ) -EnableThinking $false
    $results | Tee-Object -Variable verifiedResults | Format-List
    $verifiedResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logDirectory 'results.json') -Encoding utf8
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}
