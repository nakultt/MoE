$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path $PSScriptRoot -Parent) 'settings.ps1')
$bench = Join-Path $LlamaRuntimeDir 'llama-bench.exe'
$model = $QwenModelPath

& $bench `
    -m $model `
    -p 512 `
    -n 128 `
    -r 3 `
    -ngl 999 `
    -ncmoe 30 `
    -ctk q8_0 `
    -ctv q8_0 `
    -fa on `
    -t 16 `
    -C 0xFFFF `
    --cpu-strict 1 `
    --prio 2

exit $LASTEXITCODE
