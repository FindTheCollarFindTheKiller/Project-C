param(
	[string]$VenvPath = ".venv"
)

$ErrorActionPreference = "Stop"

function Resolve-PythonLauncher {
	if (Get-Command py -ErrorAction SilentlyContinue) {
		try {
			py -3.13 --version *> $null
			if ($LASTEXITCODE -eq 0) {
				return "py -3.13"
			}
		}
		catch {}
	}

	if (Get-Command python -ErrorAction SilentlyContinue) {
		try {
			python -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 13) else 1)" *> $null
			if ($LASTEXITCODE -eq 0) {
				return "python"
			}
		}
		catch {}
	}

	throw "Python 3.13+ not found. Install Python 3.13 and ensure 'py -3.13' or 'python' is available."
}

$launcher = Resolve-PythonLauncher
Write-Host "Using launcher: $launcher"

& cmd /c "$launcher -m venv \"$VenvPath\""
if ($LASTEXITCODE -ne 0) {
	throw "Failed to create virtual environment at '$VenvPath'."
}

$pythonExe = Join-Path $VenvPath "Scripts\python.exe"
if (-not (Test-Path $pythonExe)) {
	throw "Virtual environment python not found at '$pythonExe'."
}

& $pythonExe -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) {
	throw "Failed to upgrade pip."
}

& $pythonExe -m pip install -r requirements.txt
if ($LASTEXITCODE -ne 0) {
	throw "Failed to install requirements.txt dependencies."
}

Write-Host "Done. Activate with: $VenvPath\Scripts\Activate.ps1"
