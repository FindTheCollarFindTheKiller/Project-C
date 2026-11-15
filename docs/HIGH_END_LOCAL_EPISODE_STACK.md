# High-End Local Episode Stack (24+ GB VRAM)

This project already supports local media generation tasks.
This guide adds optional components for a higher-end fully local workflow.

## What this adds

- Stronger local script/model runtimes (Ollama/vLLM/llama.cpp helpers)
- Higher-quality local TTS options (XTTS via `TTS` package)
- Local music/SFX generation support packages
- Lip-sync helper dependencies
- One-command setup script for this profile

## Quick setup

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup_high_end_local_stack.ps1
```

To skip optional packages or model pulls:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup_high_end_local_stack.ps1 -InstallOptionalPackages:$false -PullOllamaModels:$false
```

## Files added

- `requirements.highend.optional.txt` — optional high-end packages
- `scripts/setup_high_end_local_stack.ps1` — orchestration script for base + optional setup

## Notes

- Base required runtime remains in `requirements.txt`.
- Some optional packages may require CUDA/toolchain alignment and can fail on specific Python/CUDA combinations.
- If optional install warnings appear, install those components manually for your exact GPU/CUDA setup.
