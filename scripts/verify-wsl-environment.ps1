[CmdletBinding()]
param(
    [string]$Distribution = 'Ubuntu',
    [switch]$ConfigureWslConfig
)

$ErrorActionPreference = 'Stop'

Write-Host "=== WSL 2 Environment Diagnostic & Setup ===" -ForegroundColor Cyan

# 1. Verify wsl.exe
if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
    throw "wsl.exe was not found. Install WSL 2 with: wsl --install -d $Distribution"
}

# 2. Verify distribution
$wslList = & wsl.exe --list --quiet 2>$null
$installedDistros = $wslList | ForEach-Object { $_.Trim() -replace "\0", "" } | Where-Object { $_ }
if ($Distribution -notin $installedDistros) {
    Write-Warning "WSL distribution '$Distribution' was not found in installed distros ($($installedDistros -join ', '))."
    Write-Host "Install it with: wsl --install -d $Distribution" -ForegroundColor Yellow
} else {
    Write-Host "[OK] Distribution '$Distribution' is installed." -ForegroundColor Green
}

# 3. Detect WSL username
$wslUser = (& wsl.exe --distribution $Distribution --exec /bin/bash -lc "whoami" 2>$null).Trim()
if ($wslUser) {
    Write-Host "[OK] Detected WSL Username: $wslUser" -ForegroundColor Green
} else {
    Write-Warning "Could not detect WSL username."
}

# 4. Test NVIDIA GPU / CUDA inside WSL
Write-Host "Checking NVIDIA GPU access inside WSL..."
$nvidiaSmi = & wsl.exe --distribution $Distribution --exec /bin/bash -lc "command -v nvidia-smi || test -x /usr/bin/nvidia-smi" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] CUDA / GPU driver is accessible inside WSL 2." -ForegroundColor Green
} else {
    Write-Warning "nvidia-smi was not found inside WSL. Ensure NVIDIA GPU drivers supporting WSL CUDA are installed on the Windows host."
}

# 5. Check %USERPROFILE%\.wslconfig
$wslConfigFile = Join-Path $env:USERPROFILE '.wslconfig'
if (Test-Path -LiteralPath $wslConfigFile) {
    $content = Get-Content -LiteralPath $wslConfigFile -Raw
    Write-Host "Found existing .wslconfig at ${wslConfigFile}:" -ForegroundColor Gray
    Write-Host $content -ForegroundColor Gray
    if ($content -notmatch 'memory\s*=') {
        Write-Warning "No explicit 'memory' limit found in .wslconfig. FastVideo requires 24+ GB RAM assigned to WSL."
    }
} else {
    Write-Warning ".wslconfig does not exist. Default WSL 2 memory limit may be insufficient for video generation."
}

# 6. Auto-configure .wslconfig if requested
if ($ConfigureWslConfig) {
    $recommendedConfig = @"
[wsl2]
memory=28GB
swap=8GB
processors=8
"@
    Set-Content -Path $wslConfigFile -Value $recommendedConfig -Encoding utf8
    Write-Host "[OK] Created/Updated ${wslConfigFile} with 28GB RAM & 8GB swap." -ForegroundColor Green
    Write-Host "Run 'wsl --shutdown' in PowerShell for changes to take effect." -ForegroundColor Yellow
}

# 7. Sync video-model.json fastVideoExecutable path with detected WSL username
if ($wslUser) {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $videoModelJsonPath = Join-Path $repoRoot 'src\MoE.ModelManager\Configuration\video-model.json'
    if (Test-Path -LiteralPath $videoModelJsonPath) {
        $json = Get-Content -LiteralPath $videoModelJsonPath -Raw | ConvertFrom-Json
        $expectedExecutable = "/home/$wslUser/.venvs/moe-fastvideo/bin/fastvideo"
        if ($json.fastVideoExecutable -ne $expectedExecutable) {
            Write-Host "Updating video-model.json fastVideoExecutable from '$($json.fastVideoExecutable)' to '$expectedExecutable'..." -ForegroundColor Yellow
            $json.fastVideoExecutable = $expectedExecutable
            $json | ConvertTo-Json -Depth 5 | Set-Content -Path $videoModelJsonPath -Encoding utf8
            Write-Host "[OK] Updated video-model.json for user '$wslUser'." -ForegroundColor Green
        } else {
            Write-Host "[OK] video-model.json fastVideoExecutable is already configured for '$wslUser'." -ForegroundColor Green
        }
    }
}

Write-Host "`nWSL diagnostic complete." -ForegroundColor Cyan
