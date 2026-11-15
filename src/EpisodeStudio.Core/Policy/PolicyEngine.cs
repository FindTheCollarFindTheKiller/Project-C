using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Core.Policy;

public sealed class PolicyEngine
{
    private static readonly string[] ViolentTerms = ["kill", "murder", "gore", "bloodbath"];
    private static readonly string[] SexualTerms = ["explicit", "sexual", "adult-only"];
    private static readonly string[] HateTerms = ["hate speech", "slur", "dehumanize"];
    private static readonly string[] SelfHarmTerms = ["self-harm", "suicide", "hurt myself"];

    public PolicyDecision Evaluate(PolicyEvaluationRequest request)
    {
        var violations = new List<string>();

        if (request.Prompt.Length > request.Profile.MaxPromptLength)
        {
            violations.Add($"Prompt exceeds max length of {request.Profile.MaxPromptLength}.");
        }

        if (request.ExpectedDurationSeconds > request.Profile.MaxOutputSeconds)
        {
            violations.Add($"Requested duration exceeds max of {request.Profile.MaxOutputSeconds} seconds.");
        }

        if (!request.Profile.AllowedProviders.Contains(request.Provider, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Provider '{request.Provider}' is not allowed by policy.");
        }

        if (!request.Profile.AllowedModels.Contains(request.Model, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Model '{request.Model}' is not allowed by policy.");
        }

        ApplyContentFilter(
            request.Prompt,
            request.Profile.AllowViolence,
            ViolentTerms,
            "Violence content is blocked.",
            violations);

        ApplyContentFilter(
            request.Prompt,
            request.Profile.AllowSexualContent,
            SexualTerms,
            "Sexual content is blocked.",
            violations);

        ApplyContentFilter(
            request.Prompt,
            request.Profile.AllowHateOrHarassment,
            HateTerms,
            "Hate or harassment content is blocked.",
            violations);

        ApplyContentFilter(
            request.Prompt,
            request.Profile.AllowSelfHarmContent,
            SelfHarmTerms,
            "Self-harm content is blocked.",
            violations);

        return violations.Count == 0
            ? PolicyDecision.Allow()
            : new PolicyDecision(false, violations);
    }

    private static void ApplyContentFilter(
        string prompt,
        bool isAllowed,
        IEnumerable<string> blockedTerms,
        string violationMessage,
        ICollection<string> violations)
    {
        if (isAllowed)
        {
            return;
        }

        if (blockedTerms.Any(term => prompt.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add(violationMessage);
        }
    }
}
