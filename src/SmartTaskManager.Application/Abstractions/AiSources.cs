namespace SmartTaskManager.Application.Abstractions;

/// <summary>Values of the "source" field on AI suggestions; part of the API contract the UI relies on.</summary>
public static class AiSources
{
    /// <summary>Produced by the configured AI model.</summary>
    public const string Ai = "ai";

    /// <summary>Produced by the offline stub because no AI API key is configured.</summary>
    public const string Fallback = "fallback";
}
