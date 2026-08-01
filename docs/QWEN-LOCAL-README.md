# Qwen3.6-35B-A3B local setup

This machine is configured to run `Qwen3.6-35B-A3B-Q4_K_M` through llama.cpp with CUDA and partial CPU expert offloading.

## Start the local server

Open PowerShell and run:

```powershell
& 'E:\Github\MoE\scripts\start-qwen3.6.ps1'
```

Then open <http://127.0.0.1:8081> in a browser. Press `Ctrl+C` in PowerShell to stop the server.

To choose another port or context size:

```powershell
& 'E:\Github\MoE\scripts\start-qwen3.6.ps1' -Port 8082 -Context 8192
```

## Benchmark

```powershell
& 'E:\Github\MoE\benchmarks\benchmark-qwen3.6.ps1'
```

Measured on this machine:

- `llama-bench tg128`: **37.17 +/- 1.14 tokens/second**
- 512-token real server generation: **34.10 tokens/second**
- GPU: RTX 4060 Laptop 8GB
- CPU: Intel Core i7-14700HX
- RAM: 31.7GB
- Model: Q4_K_M, 22,285,080,192 bytes

The important settings are `-ncmoe 30`, Q8 KV cache, one server slot, and CPU affinity `0xFFFF`, which keeps inference on the P-core logical processors.

Reasoning/thinking is disabled by default with `-rea off`, so chat responses go directly to the final answer without producing reasoning tokens.
