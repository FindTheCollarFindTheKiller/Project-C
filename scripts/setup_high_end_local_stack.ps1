param(
	[string]$VenvPath = ".venv",
	[switch]$InstallOptionalPackages = $true,
	[switch]$PullOllamaModels = $true
)

$ErrorActionPreference = "Stop"

$baseSetup = Join-Path "scripts" "setup_local_transformers.ps1"
if (-not (Test-Path $baseSetup)) {
	throw "Missing base setup script: $baseSetup"
}

Write-Host "[1/4] Installing base local transformers stack..."
& powershell -ExecutionPolicy Bypass -File $baseSetup -VenvPath $VenvPath
if ($LASTEXITCODE -ne 0) {
	throw "Base stack setup failed."
}

$pythonExe = Join-Path $VenvPath "Scripts\python.exe"
if (-not (Test-Path $pythonExe)) {
	throw "Virtual environment python not found at '$pythonExe'."
}

if ($InstallOptionalPackages) {
	Write-Host "[2/4] Installing high-end optional packages..."
	& $pythonExe -m pip install -r requirements.highend.optional.txt
	if ($LASTEXITCODE -ne 0) {
		Write-Warning "Optional package installation had issues. Some advanced components may require manual install on this machine."
	}
} else {
	Write-Host "[2/4] Skipped optional package installation."
}

Write-Host "[3/4] Checking HF CLI availability..."
& $pythonExe -m pip install "huggingface-hub[cli]"
if ($LASTEXITCODE -ne 0) {
	Write-Warning "Could not install huggingface-hub CLI extras automatically."
}

if ($PullOllamaModels -and (Get-Command ollama -ErrorAction SilentlyContinue)) {
	Write-Host "[4/4] Pulling recommended local script models with Ollama..."
	ollama pull llama3.1:8b-instruct
	ollama pull qwen2.5:14b-instruct
	ollama pull mistral:7b-instruct
} else {
	Write-Host "[4/4] Skipped Ollama model pulls (ollama not found or switch disabled)."
}

Write-Host "Done."
Write-Host "Next steps:"
Write-Host "  - Activate env: $VenvPath\Scripts\Activate.ps1"
Write-Host "  - Login to HF (for gated models): huggingface-cli login"
Write-Host "  - Keep using script_to_video/script_to_audio/script_to_episode in the app."
