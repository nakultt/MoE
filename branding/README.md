# Branding

| File | Purpose |
| --- | --- |
| `logo.svg` | Master app icon (Fluent "expert router" mark) used for 40 px and larger. |
| `logo-small.svg` | Simplified mark with heavier strokes used for 16–32 px so the icon stays legible on the taskbar and title bar. |
| `brands/` | Source logos for the model publishers shown in the app. |

Regenerate every MSIX visual asset, `AppIcon.ico`, and the model brand tiles in
`src/MoE.ModelManager/Assets` after changing any file here:

```bash
node scripts/generate-app-icons.js
```

The script needs Node.js with Playwright (Chromium) and Python 3 with Pillow.

## Third-party marks

The publisher logos identify which model a card or chat message belongs to. They remain
trademarks of their owners and are used only for that purpose.

| File | Mark | Source |
| --- | --- | --- |
| `brands/qwen.svg` | Qwen (Alibaba Cloud) | [LobeHub Icons](https://github.com/lobehub/lobe-icons) (MIT) |
| `brands/gemma.svg` | Gemma (Google) | [LobeHub Icons](https://github.com/lobehub/lobe-icons) (MIT) |
| `brands/nvidia.svg` | NVIDIA (Nemotron, Parakeet) | [LobeHub Icons](https://github.com/lobehub/lobe-icons) (MIT) |
| `brands/flux.svg` | FLUX (Black Forest Labs) | [LobeHub Icons](https://github.com/lobehub/lobe-icons) (MIT) |
| `brands/wan.png` | Wan (Alibaba Wan team) | [Wan-AI on Hugging Face](https://huggingface.co/Wan-AI) |
| `brands/kokoro.png` | Kokoro (hexgrad) | [hexgrad on Hugging Face](https://huggingface.co/hexgrad) |
