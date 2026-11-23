$RepoRoot = $PSScriptRoot
# llama.cpp build bundled with the app. Upgrade: change these two values, add the new SHA256 hashes in
# scripts\install-runtime.ps1, then run it. The folder name stays the same so the .csproj and CI need no edits.
$LlamaRuntimeRelease = 'b12651'
# CUDA 12.4 measured ~20% faster than 13.5 for llama.cpp b12651 on the RTX 4060 Laptop GPU (Nov 2026).
$LlamaRuntimeCuda = '12.4'
$LlamaRuntimeDir = Join-Path $RepoRoot 'runtime\llama.cpp'

# Large model files stay outside the repository on E:. Change only these paths if they move.
$QwenModelPath = 'E:\Models\Qwen_Qwen3.6-35B-A3B-Q4_K_M.gguf'
$QwenVisionProjectorPath = 'E:\Models\mmproj-Qwen3.6-35B-A3B-Q8_0.gguf'
$GemmaTargetModelPath = 'E:\Models\gemma4\gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf'
$GemmaVisionProjectorPath = 'E:\Models\gemma4\mmproj-F16.gguf'
$GemmaMtpQ4ModelPath = 'E:\Models\gemma4\mtp-gemma-4-26B-A4B-it.gguf'
$GemmaMtpQ8ModelPath = 'E:\Models\gemma4\MTP\mtp-gemma-4-26B-A4B-it-Q8_0.gguf'
$NemotronOmniModelPath = 'E:\Models\Nemotron-3-Nano-Omni-30B-A3B\Nemotron-3-Nano-Omni-30B-A3B-Reasoning-APEX-I-Nano.gguf'
$NemotronOmniProjectorPath = 'E:\Models\Nemotron-3-Nano-Omni-30B-A3B\mmproj-combined-F16.gguf'
$InklingModelPath = 'E:\Models\Inkling-2-Mini\Inkling-2-Mini-UD-Q4_K_XL.gguf'
$InklingVisionProjectorPath = 'E:\Models\Inkling-2-Mini\mmproj-F16.gguf'
