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

Measured on this machine after direct-decoding tuning:

- 512-token real server generation: **41.18 tokens/second median**
- Repeated samples: **37.78, 41.73, and 41.18 tokens/second**
- GPU: RTX 4060 Laptop 8GB
- CPU: Intel Core i7-14700HX
- RAM: 31.7GB
- Model: Q4_K_M, 22,285,080,192 bytes

The important settings are an 8,192-token context, `-ncmoe 30`, F16 KV cache, 14 inference threads, one server slot, and CPU affinity `0xFFFF`, which keeps inference on the P-core logical processors. The first generation after loading can be slower while caches and clock speeds warm up; sustained requests exceed 40 TPS.

Reasoning/thinking is disabled by default with `-rea off`, so chat responses go directly to the final answer without producing reasoning tokens.
