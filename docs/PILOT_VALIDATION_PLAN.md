# EpisodeStudio MVP Pilot Validation Plan

## Objective
Validate end-to-end script-to-video workflow quality, stability, and cost controls before broad rollout.

## Test Cohort
- 3-5 pilot users (creator/editor profiles)
- 10 scripts spanning 1 to 8 scenes

## Scenarios
1. Happy path: script -> scene plan -> storyboard -> voice -> render -> export
2. Safety path: disallowed prompt should be blocked with clear reason
3. Retry path: simulated stage failure then successful retry
4. Budget path: enforce warning and block/exceed behavior per project budget

## Metrics to Capture
- End-to-end success rate
- Stage-level durations and retries
- Cloud spend per project
- Crash count and top error signatures

## Exit Criteria
- >=90% successful end-to-end runs
- No critical crashes without log capture
- Policy controls behave as expected for blocked content
- Positive pilot feedback on usability and output quality
