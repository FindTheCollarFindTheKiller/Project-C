param(
	[string]$Configuration = "Release",
	[string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing EpisodeStudio desktop app..."
dotnet publish src/EpisodeStudio.Desktop/EpisodeStudio.Desktop.csproj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true

Write-Host "Publish completed."
Write-Host "MSIX note: add a Windows Packaging project and wire this publish output as payload for signed installer generation."
