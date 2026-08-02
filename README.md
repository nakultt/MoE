# MoE Model Manager

Native WinUI 3 desktop control center for local Qwen3.6, Gemma 4, and FLUX.2 Klein models. It loads one GPU model at a time, provides Markdown chat and image generation, and exposes the active model through an OpenAI-compatible loopback API.

## What is included

- Model selection, load, eject, and live runtime status.
- Streaming local chat with native Markdown rendering for headings, lists, emphasis, links, quotes, and code.
- FLUX.2 Klein image generation with size selection, in-app preview, automatic local saving, and cancel support.
- OpenAI-compatible `GET`/`POST` proxy at `http://127.0.0.1:8081/v1`.
- Request activity showing chat TPS or image generation duration for desktop and external API calls.
- Pinned CUDA 13.3 llama.cpp runtime installer (`b10218`).
- Pinned CUDA 12 stable-diffusion.cpp image runtime (`e31a86c`).
- Reproducible x64 MSIX packaging script.

The GGUF and safetensors files are intentionally not stored in this repository. The app and runtimes live on `E:`, while the large model files stay on `C:`.

## Measured configurations on this machine

| Model | Context | CPU MoE layers | Speculative decoding | Sustained result |
| --- | ---: | ---: | --- | ---: |
| Qwen3.6 35B A3B Q4_K_M | 8,192 | 30 | Off | 41.18 TPS |
| Gemma 4 26B A4B QAT UD-Q4_K_XL | 8,192 | 20 | Q8 MTP, draft length 2 | 51.51 TPS |

Gemma is one main model. Its small Q8 MTP GGUF is a speculative-decoding helper, not a second chat model. Both files are loaded together when Gemma is selected.

Short replies can report lower TPS because startup/prompt overhead dominates. Longer generations are the useful performance measurement.

### FLUX.2 Klein image performance

The image runtime uses the distilled four-step FLUX.2 Klein 4B architecture with a Q4 diffusion transformer and Q4 Qwen3 text encoder. Both run on CUDA; the VAE also stays on the GPU. The final configuration peaks near 6.34 GB VRAM and leaves headroom on the 8 GB RTX 4060.

| Image size | Confirmed warm generation time |
| --- | ---: |
| **512×512 (default)** | **11.42 seconds** |
| 768×768 | 25.88 seconds |

The official FP8 transformer was tested first but took several minutes at 512×512 in this C++ backend. The Q4 checkpoint produced good visual quality and was substantially faster. Only one LLM or image model is held on the GPU at a time; loading FLUX ejects Qwen/Gemma, and loading an LLM ejects FLUX.

### Qwen embedded-MTP comparison

Qwen's main GGUF contains an embedded MTP head, so it does not need a separate draft GGUF. On this 8 GB RTX 4060 configuration, however, embedded MTP is slower because drafting repeats CPU-offloaded expert work. The app and fallback launcher therefore keep MTP disabled for Qwen.

The end-to-end comparison used a 16,384-token context, 30 CPU MoE layers, a deterministic long-form prompt, and three 512-token generations per configuration:

| Qwen MTP draft length | Median generation speed |
| ---: | ---: |
| Off | 27.40 TPS |
| 1 | 9.48 TPS |
| 2 | 10.57 TPS |
| 3 | 10.71 TPS |
| 4 | 9.95 TPS |

That MTP comparison used the original 16K context, Q8 KV cache, and 16-thread configuration. Direct-decoding tuning subsequently raised Qwen to 41.18 TPS with an 8K context, F16 KV cache, 14 P-core threads, 30 CPU MoE layers, and MTP disabled. Re-run a configuration with `benchmarks\benchmark-qwen3.6-config.ps1`; draft length `0` remains the verified winner.

| Qwen direct-decoding configuration | Median generation speed |
| --- | ---: |
| 16K, Q8 KV, 16 threads | 27.40 TPS |
| 8K, Q8 KV, 16 threads | 35.78 TPS |
| 8K, Q4 KV, 14 threads | 36.92 TPS |
| **8K, F16 KV, 14 threads** | **41.18 TPS** |

The winning Balanced-power samples were 37.78, 41.73, and 41.18 TPS. The first generation after loading is colder; sustained requests exceed 40 TPS. Ultimate Performance produced a similar 40.01 TPS median, so the app does not change the Windows power plan.

### Gemma MTP and CPU-offload comparison

Gemma was reverified with the same deterministic long-form prompt and three 512-token generations for the competitive configurations. Q8 MTP with two draft tokens remains the fastest option:

| Gemma draft configuration | CPU MoE layers | Median generation speed |
| --- | ---: | ---: |
| None | 20 | 31.27 TPS |
| Q4 MTP, length 2 | 20 | 48.95 TPS |
| Q8 MTP, length 1 | 20 | 42.34 TPS |
| **Q8 MTP, length 2** | **20** | **51.51 TPS** |
| Q8 MTP, length 3 | 20 | 38.56 TPS |
| Q8 MTP, length 4 | 20 | 34.23 TPS |

The winning Q8 length-2 configuration was also screened with 18–22 CPU MoE layers. Twenty layers remained fastest; moving in either direction reduced TPS or left too little VRAM headroom. Re-run an individual combination with `benchmarks\benchmark-gemma-config.ps1 -CpuMoeLayers <0-30> -Draft <none|q4|q8> -DraftTokens <1-8>`.

## Run from source

Requirements:

- Windows 11 x64
- NVIDIA RTX GPU with a current driver
- .NET 10 SDK and the Windows application development workload

Install or verify the pinned local runtime:

```powershell
Set-Location E:\Github\MoE
.\scripts\install-runtime.ps1
.\scripts\install-image-model.ps1
```

Build and launch:

```powershell
dotnet build .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Debug -p:Platform=x64
dotnet run --project .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Debug -p:Platform=x64
```

If an LLM GGUF moves, edit its absolute path in [models.json](src/MoE.ModelManager/Configuration/models.json). FLUX paths and its four-step configuration live in [image-model.json](src/MoE.ModelManager/Configuration/image-model.json).

## Fallback llama.cpp Web UI launchers

The desktop app is the normal launcher. These scripts are available as a direct llama.cpp fallback and expose both llama.cpp's built-in Web UI and its OpenAI-compatible API.

Gemma with the verified 51.51 TPS Q8 MTP configuration:

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

Text model IDs are `qwen3.6-35b-a3b` and `gemma-4-26b-a4b`; the image model ID is `flux.2-klein-4b`. Requests from built-in chat/image generation are labeled **Desktop**; requests from other processes are labeled **External**.

Load FLUX in the app, then generate an image through the same API:

```python
from base64 import b64decode
from openai import OpenAI

client = OpenAI(base_url="http://127.0.0.1:8081/v1", api_key="local")
result = client.images.generate(
    model="flux.2-klein-4b",
    prompt="A tiny glass greenhouse on a mossy forest floor at sunrise",
    size="512x512",
    response_format="b64_json",
)
with open("flux.png", "wb") as output:
    output.write(b64decode(result.data[0].b64_json))
```

The image endpoint is `POST /v1/images/generations`. Responses contain standard `data[].b64_json` image data and appear in the app's Request activity panel.

## Build the installer

```powershell
Set-Location E:\Github\MoE
.\scripts\build-installer.ps1
```

Output:

- `artifacts\MoE.ModelManager_1.1.0.1_x64.msix`
- `artifacts\MoE.ModelManager-devcert.cer`

This is a locally signed development installer. Install it with:

```powershell
.\scripts\install-app.ps1
```

Approve the Windows administrator prompt, then select **Install** in App Installer. The helper verifies that the certificate matches the MSIX signer and imports it into **Local Computer → Trusted People**, which is the machine store App Installer checks. Do not choose the Current User certificate store. The private `.pfx`, generated output, runtime binaries, and model files are gitignored.

## Verification

```powershell
dotnet build .\src\MoE.ModelManager\MoE.ModelManager.csproj -c Release -p:Platform=x64
dotnet test .\src\MoE.ModelManager.Tests\MoE.ModelManager.Tests.csproj -c Debug
```

The Release build and focused tests cover text/image model configuration, llama.cpp and stable-diffusion.cpp arguments, invalid ports, and bounded request telemetry.
