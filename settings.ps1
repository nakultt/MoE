$RepoRoot = $PSScriptRoot
$LlamaRuntimeDir = Join-Path $RepoRoot 'runtime\llama.cpp-b10218-cuda13'

# Large model files stay on C:. Change only these paths if the GGUF files move.
$QwenModelPath = 'C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models\Qwen_Qwen3.6-35B-A3B-Q4_K_M.gguf'
$GemmaTargetModelPath = 'C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models\gemma4\gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf'
$GemmaMtpQ4ModelPath = 'C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models\gemma4\mtp-gemma-4-26B-A4B-it.gguf'
$GemmaMtpQ8ModelPath = 'C:\Users\nakul\Documents\Codex\2026-08-01\to-get-40-tokens-per-second\work\models\gemma4\MTP\mtp-gemma-4-26B-A4B-it-Q8_0.gguf'
