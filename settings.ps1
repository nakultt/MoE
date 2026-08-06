$RepoRoot = $PSScriptRoot
$LlamaRuntimeDir = Join-Path $RepoRoot 'runtime\llama.cpp-b10218-cuda13'

# Large model files stay on C:. Change only these paths if the GGUF files move.
$QwenModelPath = 'E:\Models\Qwen_Qwen3.6-35B-A3B-Q4_K_M.gguf'
$QwenVisionProjectorPath = 'E:\Models\mmproj-Qwen3.6-35B-A3B-Q8_0.gguf'
$GemmaTargetModelPath = 'E:\Models\gemma4\gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf'
$GemmaVisionProjectorPath = 'E:\Models\gemma4\mmproj-F16.gguf'
$GemmaMtpQ4ModelPath = 'E:\Models\gemma4\mtp-gemma-4-26B-A4B-it.gguf'
$GemmaMtpQ8ModelPath = 'E:\Models\gemma4\MTP\mtp-gemma-4-26B-A4B-it-Q8_0.gguf'
