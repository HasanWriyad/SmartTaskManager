namespace SmartTaskManager.Application.Features.Projects;

public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description);

public sealed record AddProjectMemberRequest(Guid UserId);

/// <param name="CanManage">Whether the caller may update, delete, or change members (drives UI actions).</param>
public sealed record ProjectListItemDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    bool CanManage,
    DateTimeOffset CreatedAt);

/// <param name="CanManage">Whether the caller may update, delete, or change members (drives UI actions).</param>
public sealed record ProjectDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    IReadOnlyList<ProjectMemberDto> Members,
    int TaskCount,
    bool CanManage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ProjectMemberDto(Guid UserId, string FullName, string Email, DateTimeOffset AddedAt);
