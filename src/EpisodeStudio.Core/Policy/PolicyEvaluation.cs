using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Core.Policy;

public sealed record PolicyEvaluationRequest(
    PolicyProfile Profile,
    string Prompt,
    string Provider,
    string Model,
    int ExpectedDurationSeconds);

public sealed record PolicyDecision(
    bool IsAllowed,
    IReadOnlyList<string> Violations)
{
    public static PolicyDecision Allow() => new(true, Array.Empty<string>());

    public static PolicyDecision Deny(params string[] violations) =>
        new(false, violations.ToList());
}
