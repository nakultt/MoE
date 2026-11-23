# Adding a new model to MoE Model Manager

Checklist for adding a new llama.cpp (GGUF) chat model. Inkling 2 Mini (November 2026) is the worked example.
Every step names the exact file to touch. Image models are covered in [Adding an image model](#adding-an-image-model);
video and audio models use their own single-entry catalogs (`video-model.json`, `audio-model.json`).

The app needs no new C# code for a normal GGUF chat model. The catalog drives the model list, the
llama-server arguments, the `/moe/*` control API, `/v1/models`, the chat model picker, and the logo.

## 1. Research the model

Find these facts before downloading anything:

| Question | Where to look | Inkling 2 Mini answer |
| --- | --- | --- |
| Architecture and total / active parameters | Official model card | Looped MoE, 26.2B total, 3.4B active, 24 layers (6–17 run 3×) |
| Which GGUF repo works with **mainline** ggml-org llama.cpp | Hugging Face GGUF repos (`ggml-org`, `unsloth`, `bartowski`) | `unsloth/Inkling-2-Mini-GGUF` → `Mainline_Support/` (root files need Unsloth's fork) |
| Minimum llama.cpp build | GGUF README, llama.cpp PRs (`gh search prs --repo ggml-org/llama.cpp <name> --merged`) | b12611 or newer (PR #32704) |
| Vision projector (`mmproj`) | Same repo | `mmproj-F16.gguf` |
| Speculative draft (MTP) file and the `--spec-type` it needs | Same repo, `llama-server --help` lists supported types | DSpark drafter, `Mainline_Support/dspark-…gguf_file`, `draft-dspark` |
| Recommended sampling, reasoning switch | Model card | temp 1.0, top-p 1.0; `reasoning_effort` |

Choose a quant that fits **8 GB VRAM + 32 GB RAM**: about 13–20 GB of GGUF for a 25–35B MoE model.
Q4_K_XL / Q4_K_M class quants are the sweet spot. Bigger files force more experts onto the CPU and lose speed.

## 2. Upgrade llama.cpp if the model needs a newer build

The bundled runtime is pinned in two places:

1. `settings.ps1` → `$LlamaRuntimeRelease` (for example `b12651`) and `$LlamaRuntimeCuda` (`12.4` or `13.5`).
2. `scripts/install-runtime.ps1` → `$expectedHashes`: add the SHA256 of
   `llama-<release>-bin-win-cuda-<cuda>-x64.zip` and `cudart-llama-bin-win-cuda-<cuda>-x64.zip`.
   Get them with:

   ```powershell
   gh api repos/ggml-org/llama.cpp/releases/tags/b12651 --jq '.assets[] | select(.name|test("win-cuda")) | "\(.name) \(.digest)"'
   ```

Then run `.\scripts\install-runtime.ps1`. It replaces `runtime\llama.cpp` when the version marker differs.
The folder name never changes, so the `.csproj` and the release workflow need no edits.

**After any runtime upgrade, re-benchmark every existing model** (step 5). A new build can change flag names or speed.
Run `runtime\llama.cpp\llama-server.exe --help` and check that every flag in
`src/MoE.ModelManager/Services/LlamaArgumentsBuilder.cs` still exists.

To compare two builds without touching the bundled one:

```powershell
.\scripts\install-runtime.ps1 -Destination runtime\llama.cpp-test -Cuda 13.5
.\benchmarks\benchmark-model.ps1 -ModelId gemma-4-26b-a4b -RuntimeDir runtime\llama.cpp-test
```

## 3. Download the weights

Model files live on `E:\Models\<ModelName>\`, never in the repository.

1. Add a download section to `scripts/setup-llm-models.ps1` (copy the Inkling block: skip-if-present, `hf download`, rename).
2. Add path variables to `settings.ps1` (`$InklingModelPath`, `$InklingVisionProjectorPath`).
3. Run the script. With `hf download`, pass each file as its own positional argument; `--include` is ignored
   when a filename is also given.

## 4. Add the catalog entry

Append an object to `src/MoE.ModelManager/Configuration/models.json`:

```json
{
  "id": "inkling-2-mini",
  "displayName": "Inkling 2 Mini 26.4B",
  "description": "One-line summary with the measured TPS.",
  "modelPath": "E:\\Models\\Inkling-2-Mini\\Inkling-2-Mini-UD-Q4_K_XL.gguf",
  "visionProjectorPath": "E:\\Models\\Inkling-2-Mini\\mmproj-F16.gguf",
  "visionImageTokens": 0,
  "supportsReasoning": true,
  "contextSize": 32768,
  "cpuMoeLayers": 14,
  "cacheType": "q8_0",
  "threads": 16,
  "draftModelPath": null,
  "draftType": "draft-mtp",
  "draftTokens": 0,
  "measuredTokensPerSecond": 0,
  "extraArguments": []
}
```

| Field | Meaning |
| --- | --- |
| `id` | Public API model id. Lowercase, dashes. It also picks the logo (step 6). **Never rename it later**; API clients use it. |
| `visionProjectorPath` | `null` for text-only models. |
| `visionImageTokens` | Fixed image-token budget for dynamic-resolution vision models (Qwen uses 1024). `0` = model default. |
| `supportsAudio` / `supportsVideo` | Only when the mmproj has audio/video encoders (Nemotron Omni). |
| `supportsReasoning` | `true` when the chat template has a thinking mode. Sends `-rea auto`. |
| `cpuMoeLayers` | `-ncmoe`: how many leading layers keep their experts in system RAM. The main speed knob (step 5). |
| `cacheType` | KV cache type: `f16`, `q8_0`, or `q4_0`. |
| `draftModelPath`, `draftTokens` | Speculative draft model and max draft length. `null` / `0` disables it. |
| `draftType` | `--spec-type` for the drafter: `draft-mtp` (default, Gemma), `draft-dspark` (Inkling), `draft-eagle3`, … |
| `extraArguments` | Extra llama-server arguments appended last, for model-specific flags (sampling defaults, `-ot` overrides). |

The first entry in the file is the default model.

## 5. Benchmark and tune

`benchmarks/benchmark-model.ps1` starts llama-server with the same arguments as the app, reading the catalog entry:

```powershell
.\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini
.\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini -CpuMoeLayers 12
.\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini -Context 65536 -CacheType q4_0
```

Check first that VRAM use is real. If `gpu_memory_mib` sits near 7,600 MiB whatever `cpuMoeLayers` is,
VRAM has overflowed into shared memory and every result is slow. Start llama-server once with `-lv 4` and read
the `CUDA0 model buffer`, `KV buffer` and `compute buffer` lines. Inkling taught two lessons:

- **Fused expert tensors.** `-ncmoe` only matches `ffn_up/gate/down_exps`. Models with fused
  `ffn_gate_up_exps` (Inkling) need an explicit override in `extraArguments`, with `cpuMoeLayers: 0`:
  `"-ot", "blk\\.(0|1|...)\\.ffn_(gate_up|up|gate|down)_exps.*=CPU"`. List tensor names with
  `python -c "from gguf import GGUFReader; ..."` (`pip install gguf`).
- **Huge compute buffer.** Inkling reserved 9.9 GB at the default micro-batch. `"-ub", "128", "--no-op-offload"`
  cut it to 0.84 GB.
- **Looped layers first.** Put the experts of layers that run several times per token on the GPU first.

Tuning order on the 8 GB RTX 4060:

1. **`cpuMoeLayers`**: start high (all layers), lower it until load fails or `gpu_memory_mib` passes ~7,600 MiB.
   The fastest value is usually the lowest one that still loads with ~400 MiB VRAM spare.
2. **`threads`**: try 14 and 16.
3. **`contextSize` / `cacheType`**: raise context while TPS holds; drop to `q4_0` KV if VRAM runs out.
4. **Draft model**: compare no drafter against draft lengths 1–4. With experts on the CPU, drafting can be slower
   (Qwen's MTP was), so keep it only if TPS rises:

   ```powershell
   .\benchmarks\benchmark-model.ps1 -ModelId inkling-2-mini -DraftModel E:\Models\Inkling-2-Mini\dspark-Inkling-2-Mini-Q8_0.gguf -DraftType draft-dspark -DraftTokens 3
   ```

Write the winning values and `measuredTokensPerSecond` into `models.json`. Add a row to the
"Measured configurations" table in `README.md`.

## 6. Logo

Model cards and chat bubbles show the publisher logo picked by a keyword in the model id.

1. Put the source logo in `branding/brands/` and list it in `branding/README.md`.
2. Add a row to `brands` in `scripts/generate-app-icons.js` and run `node scripts/generate-app-icons.js`.
   This writes the 96 px tile to `src/MoE.ModelManager/Assets/Brands/`.
3. Add `("<keyword>", "<file>.png", "<Publisher>")` to `src/MoE.ModelManager/Services/ModelBrandCatalog.cs`.
   The first keyword contained in the id wins, so keep keywords distinct.
4. Add a `DataRow` to `src/MoE.ModelManager.Tests/ModelBrandCatalogTests.cs`.

## 7. Check reasoning and chat behaviour

`OpenAiProxyService` sends `chat_template_kwargs.enable_thinking=false` unless the client sets it.
Load the model and confirm both modes work:

```powershell
$body = '{"model":"inkling-2-mini","messages":[{"role":"user","content":"What is 17*23?"}],"max_tokens":300}'
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:8081/v1/chat/completions -ContentType application/json -Body $body
```

The response must have non-empty `content`. Repeat with `"chat_template_kwargs":{"enable_thinking":true}`
and check that `reasoning_content` fills. If the template ignores `enable_thinking`, note that in the README.

## 8. Docs, tests, release

1. `README.md`: intro model list, setup step 4, measured configurations table, model identifiers table, `/v1` prerequisite list.
2. `C:\Users\nakul\.claude\skills\moe-models\SKILL.md`: add the model id (other projects read this skill).
3. Build and test:

   ```powershell
   dotnet build MoE.slnx -c Release
   dotnet test src\MoE.ModelManager.Tests\MoE.ModelManager.Tests.csproj -c Release
   ```

4. Commit and push to `main`. The release workflow builds and publishes the signed MSIX.
5. Install the local build: `.\scripts\install-app.ps1`.

## Adding an image model

FLUX.3 Image Klein (November 2026) is the worked example. Image models run in stable-diffusion.cpp
(`runtime\stable-diffusion.cpp`, pinned in `scripts/install-image-runtime.ps1`).

1. **Check runtime support.** Search the stable-diffusion.cpp docs (`docs/models/<name>.md`) and releases for the
   model. FLUX.3 needed `master-968` or newer. To upgrade, change `$release`, `$archiveName` and `$archiveHash` in
   `scripts/install-image-runtime.ps1` (hash from
   `gh api repos/leejet/stable-diffusion.cpp/releases/tags/<tag> --jq '.assets[] | "\(.name) \(.digest)"'`),
   then run it with `-Destination runtime\stable-diffusion.cpp-test` first to benchmark without touching the app.
   Run `sd-server.exe --help` and check that every flag in
   `src/MoE.ModelManager/Services/ImageRuntimeArgumentsBuilder.cs` still exists; renamed flags are listed as "deprecated".
2. **Download** the diffusion GGUF (Q4_K class fits 8 GB), the text encoder GGUF and the VAE into
   `E:\Models\<name>\`. Add the downloads to `scripts/install-image-model.ps1`.
3. **Add the entry** to `src/MoE.ModelManager/Configuration/image-models.json`. The first entry is the default.
   `steps` comes from the model docs (distilled FLUX.3 Klein: 16, FLUX.2 Klein: 4).
   `extraArguments` holds per-model sd-server flags such as `--sage-attn`.
4. **Benchmark** each size with and without candidate flags, and look at the images in `logs\`:

   ```powershell
   .\benchmarks\benchmark-flux.ps1 -ModelId flux.3-image-klein -Size 512x512
   .\benchmarks\benchmark-flux.ps1 -ModelId flux.3-image-klein -Size 512x512 -PerfArgs @('--flash-attn','off,diffusion=on','--conv-direct','auto,vae=on','--sage-attn')
   ```

   Keep a flag only for the models it speeds up (SageAttention helps FLUX.2 by ~30% and slows FLUX.3).
5. **Logo:** the id keyword picks it (`flux` → Black Forest Labs), as in step 6 above.
6. **Docs:** update the "Image model performance" table and the model identifiers table in `README.md`, and the
   model list in `C:\Users\nakul\.claude\skills\moe-models\SKILL.md`. Then build, test, commit and install as in step 8.
