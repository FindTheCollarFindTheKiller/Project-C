# EpisodeStudio MVP Release Checklist

## Build & Quality Gates
- [ ] `dotnet build EpisodeStudio.slnx` passes
- [ ] `dotnet test tests/EpisodeStudio.Tests/EpisodeStudio.Tests.csproj` passes
- [ ] Smoke run desktop workflow: project setup -> script -> scene plan -> generation -> export

## Security & Secrets
- [ ] API keys stored via DPAPI-backed key store only
- [ ] No secrets in source control or logs
- [ ] Validate policy profile defaults and blocked-content behavior

## Reliability
- [ ] Confirm crash log output under `%LOCALAPPDATA%/EpisodeStudio/logs/crash.log`
- [ ] Verify failed stage retry path in generation workflow
- [ ] Validate recovery behavior for unexpected app termination

## Packaging & Distribution
- [ ] Publish x64 build via `scripts/publish-mvp.ps1`
- [ ] Package installer (MSIX) with signed certificate in release pipeline
- [ ] Validate install/uninstall on clean Windows VM

## Pilot Validation
- [ ] Run 10 representative scripts (short/medium/long)
- [ ] Record success rate, median runtime, and failure causes
- [ ] Capture user feedback from pilot creators
- [ ] Confirm budget alerts trigger near threshold and on exceed
