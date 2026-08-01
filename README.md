# MoE Model Manager

Native WinUI 3 desktop control center for the local Qwen3.6 35B A3B and Gemma 4 26B A4B GGUF models. It loads one optimized model at a time, provides a Markdown-capable chat UI, and exposes the loaded model through an OpenAI-compatible loopback API.

## What is included

- Model selection, load, eject, and live runtime status.
- Streaming local chat with native Markdown rendering for headings, lists, emphasis, links, quotes, and code.
- OpenAI-compatible `GET`/`POST` proxy at `http://127.0.0.1:8081/v1`.
- Request activity showing source, endpoint, prompt/generated tokens, duration, and measured TPS.
- Pinned CUDA 13.3 llama.cpp runtime installer (`b10218`).
- Reproducible x64 MSIX packaging script.

The GGUF files are intentionally not stored in this repository. The app and llama.cpp runtime live on `E:`, while the large model files stay on `C:`.

## Measured configurations on this machine

| Model | Context | CPU MoE layers | Speculative decoding | Sustained result |
| --- | ---: | ---: | --- | ---: |
| Qwen3.6 35B A3B Q4_K_M | 16,384 | 30 | Off | 34.10 TPS |
| Gemma 4 26B A4B QAT UD-Q4_K_XL | 8,192 | 20 | Q8 MTP, draft length 2 | 50.65 TPS |

Gemma is one main model. Its small Q8 MTP GGUF is a speculative-decoding helper, not a second chat model. Both files are loaded together when Gemma is selected.

Short replies can report lower TPS because startup/prompt overhead dominates. Longer generations are the useful performance measurement.

## Run from source

Requirements:

- Windows 11 x64
- NVIDIA RTX GPU with a current driver
- .NET 10 SDK and the Windows application development workload

Install or verify the pinned local runtime:

```powershell
Set-Location E:\Github\MoE
.\scripts\install-runtime.ps1
```

Build and launch:

```powershell
dotnet build .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Debug -p:Platform=x64
dotnet run --project .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Debug -p:Platform=x64
```

If a GGUF moves, edit its absolute path in [models.json](src/MoE.ModelManager/Configuration/models.json). Model-specific context, CPU MoE layers, MTP path, and measured TPS are stored in the same file.

## Fallback llama.cpp Web UI launchers

The desktop app is the normal launcher. These scripts are available as a direct llama.cpp fallback and expose both llama.cpp's built-in Web UI and its OpenAI-compatible API.

Gemma with the verified 50.65 TPS Q8 MTP configuration:

```powershell
Set-Location E:\Github\MoE
.\scripts\start-gemma4.ps1
```

Qwen:

```powershell
.\scripts\start-qwen3.6.ps1
```

Both default to `http://127.0.0.1:8081`. Close the MoE Model Manager or choose another `-Port` before using a fallback launcher. Gemma accepts optional `-Context`, `-CpuMoeLayers`, and `-DraftTokens` parameters, but its defaults are the measured winning configuration.

## Use the OpenAI-compatible API

Load a model in the app first. The API listens only on loopback and does not require an API key.

Python with the OpenAI client:

```python
from openai import OpenAI

client = OpenAI(base_url="http://127.0.0.1:8081/v1", api_key="local")
response = client.chat.completions.create(
    model="gemma-4-26b-a4b",
    messages=[{"role": "user", "content": "Explain MoE models briefly."}],
)
print(response.choices[0].message.content)
```

PowerShell:

```powershell
$body = @{
    model = 'gemma-4-26b-a4b'
    messages = @(@{ role = 'user'; content = 'Give me three GPU inference tips.' })
    stream = $false
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
    -Uri 'http://127.0.0.1:8081/v1/chat/completions' `
    -Method Post `
    -ContentType 'application/json' `
    -Body $body
```

Available model IDs are `qwen3.6-35b-a3b` and `gemma-4-26b-a4b`. Requests from the built-in chat are labeled **Desktop**; requests from other processes are labeled **External**.

## Build the installer

```powershell
Set-Location E:\Github\MoE
.\scripts\build-installer.ps1
```

Output:

- `artifacts\MoE.ModelManager_1.0.0.0_x64.msix`
- `artifacts\MoE.ModelManager-devcert.cer`

This is a locally signed development installer. Trust the included public `.cer` in the Windows **Trusted People** certificate store, then open the `.msix`. The private `.pfx`, generated output, runtime binaries, and model files are gitignored.

## Verification

```powershell
dotnet build .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Release -p:Platform=x64
dotnet test .\src\MoE.ModelManager.Tests\MoE.ModelManager.Tests.csproj -c Debug
```

The current Release build completes with zero warnings, and the focused test suite covers model configuration, llama.cpp arguments, invalid ports, and bounded request telemetry.
