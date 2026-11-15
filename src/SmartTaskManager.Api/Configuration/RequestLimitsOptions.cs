using System.ComponentModel.DataAnnotations;

namespace SmartTaskManager.Api.Configuration;

public sealed class RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    /// <summary>
    /// Largest accepted request body (default 1 MB). The largest legitimate request is a few KB; larger
    /// bodies get 413. Kestrel's own default (30 MB) only invites abuse.
    /// </summary>
    [Range(1024, 30 * 1024 * 1024)]
    public long MaxRequestBodyBytes { get; init; } = 1024 * 1024;
}
