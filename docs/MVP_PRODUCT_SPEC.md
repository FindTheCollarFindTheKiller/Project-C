# EpisodeStudio MVP Product Specification

## Goal
Build a Windows desktop application that converts a script into a short AI-generated episode/movie draft with editable scenes and MP4 export.

## Target Users
- Indie creators
- Small media teams
- Internal pre-visualization teams

## MVP User Stories
1. As a creator, I can create a project and paste a script.
2. As a creator, I can generate scene and shot breakdowns from the script.
3. As a creator, I can generate storyboard image placeholders for each shot.
4. As a creator, I can generate narration/dialog voice tracks.
5. As a creator, I can review and edit scenes/shots before final render.
6. As a creator, I can run a render job and export an MP4.
7. As an admin, I can configure safety policy, allowed providers, and model settings.
8. As an admin, I can set per-project cloud budget limits.

## MVP Functional Requirements
- Project management (create/open/save)
- Script ingestion and validation
- Pipeline stages with resumable checkpoints:
  - ScriptParse
  - ScenePlan
  - StoryboardGen
  - VoiceGen
  - TimelineCompose
  - RenderExport
- Provider abstraction for LLM/Image/TTS backends
- Admin safety and governance policy system
- Job history with status and errors
- MP4 export

## Non-Functional Requirements
- Performance: first draft generation starts in under 10s after submit
- Reliability: resumable pipeline with stage-level retries
- Security: API keys stored securely in user profile, never plaintext in logs
- Cost: enforce configurable budget limits and usage telemetry
- Observability: structured logs and stage metrics

## Acceptance Criteria
- A script can be transformed into a renderable timeline
- Users can edit scene/shot text before render
- Output MP4 is produced for a successful job
- Safety policy blocks disallowed prompts/providers
- Failed stage can be retried without restarting all prior stages

## Out of Scope (MVP)
- Full NLE-grade timeline editor
- Multi-user collaboration
- Real-time co-editing
- Model fine-tuning

## Success Metrics
- 90%+ successful end-to-end pipeline runs for validation scripts
- 95th percentile job completion time tracked and visible
- Zero secret values written to logs
