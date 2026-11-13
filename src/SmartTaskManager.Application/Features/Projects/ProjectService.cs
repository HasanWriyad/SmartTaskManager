using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Projects;

internal sealed partial class ProjectService(
    IProjectRepository projects,
    ITaskRepository tasks,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ProjectService> logger) : IProjectService
{
    public Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken) =>
        projects.ListVisibleAsync(currentUser.RequireViewer(), query, cancellationToken);

    public async Task<ProjectDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetDetailsAsync(id, currentUser.RequireViewer(), cancellationToken)
        ?? throw ProjectNotFound(id);

    public async Task<ProjectDetailsDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        if (!ProjectAccess.CanCreate(viewer))
        {
            throw new ForbiddenException("Only Admins and Project Managers can create projects.");
        }

        var project = Project.Create(request.Name, request.Description, viewer.UserId);
        await EnsureNameAvailableAsync(project.OwnerId, project.Name, excludingProjectId: null, cancellationToken);

        projects.Add(project); // the key is generated on Add, so the membership can reference it
        // The owner is a member too, so they can be assigned tasks; both rows go in one save.
        projects.AddMember(ProjectMember.Create(project.Id, project.OwnerId, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogProjectCreated(logger, viewer.UserId, project.Id);
        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var (project, viewer) = await LoadForManageAsync(id, cancellationToken);

        project.Update(request.Name, request.Description);
        await EnsureNameAvailableAsync(project.OwnerId, project.Name, project.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogProjectUpdated(logger, viewer.UserId, project.Id);
        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var (project, viewer) = await LoadForManageAsync(id, cancellationToken);

        // The soft-delete interceptor converts this into IsDeleted on the project and all its
        // live tasks, written by this single SaveChanges (one transaction).
        projects.Remove(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogProjectDeleted(logger, viewer.UserId, project.Id);
    }

    public async Task<ProjectDetailsDto> AddMemberAsync(Guid id, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var (project, viewer) = await LoadForManageAsync(id, cancellationToken);

        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);
        if (!user.IsActive)
        {
            throw new ConflictException("Inactive users cannot be added to a project.");
        }

        if (await projects.IsMemberAsync(project.Id, user.Id, cancellationToken))
        {
            throw new ConflictException("The user is already a member of this project.");
        }

        projects.AddMember(ProjectMember.Create(project.Id, user.Id, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogMemberAdded(logger, viewer.UserId, user.Id, project.Id);
        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> RemoveMemberAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var (project, viewer) = await LoadForManageAsync(id, cancellationToken);

        var member = await projects.GetMemberAsync(project.Id, userId, cancellationToken)
            ?? throw new NotFoundException("Project member", userId);
        if (member.UserId == project.OwnerId)
        {
            throw new ConflictException("The project owner cannot be removed from the members.");
        }

        // Assignees must be members, so the removed user's tasks in this project become unassigned.
        var assigned = await tasks.GetAssignedInProjectAsync(project.Id, userId, cancellationToken);
        foreach (var task in assigned)
        {
            task.AssignTo(null);
        }

        projects.RemoveMember(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogMemberRemoved(logger, viewer.UserId, userId, project.Id, assigned.Count);
        return await GetAsync(project.Id, cancellationToken);
    }

    /// <summary>404 if the project is missing or invisible to the caller; 403 if visible but not manageable.</summary>
    private async Task<(Project Project, Viewer Viewer)> LoadForManageAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        var project = await projects.GetVisibleAsync(id, viewer, cancellationToken) ?? throw ProjectNotFound(id);

        if (!ProjectAccess.CanManage(viewer, project.OwnerId))
        {
            throw new ForbiddenException(ProjectAccess.ManageForbiddenMessage);
        }

        return (project, viewer);
    }

    private async Task EnsureNameAvailableAsync(Guid ownerId, string name, Guid? excludingProjectId, CancellationToken cancellationToken)
    {
        if (await projects.NameExistsAsync(ownerId, name, excludingProjectId, cancellationToken))
        {
            throw new ConflictException("The owner already has a project with this name.");
        }
    }

    private static NotFoundException ProjectNotFound(Guid id) => new("Project", id);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} created project {ProjectId}")]
    private static partial void LogProjectCreated(ILogger logger, Guid actorId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} updated project {ProjectId}")]
    private static partial void LogProjectUpdated(ILogger logger, Guid actorId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} deleted project {ProjectId}")]
    private static partial void LogProjectDeleted(ILogger logger, Guid actorId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} added member {UserId} to project {ProjectId}")]
    private static partial void LogMemberAdded(ILogger logger, Guid actorId, Guid userId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "User {ActorId} removed member {UserId} from project {ProjectId}; unassigned {TaskCount} task(s)")]
    private static partial void LogMemberRemoved(ILogger logger, Guid actorId, Guid userId, Guid projectId, int taskCount);
}
