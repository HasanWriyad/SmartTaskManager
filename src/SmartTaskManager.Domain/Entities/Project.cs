using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Domain.Entities;

public sealed class Project : SoftDeletableEntity
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectMember> _members = [];
    private readonly List<TaskItem> _tasks = [];

    private Project()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public User Owner { get; private set; } = null!;

    public IReadOnlyCollection<ProjectMember> Members => _members;

    public IReadOnlyCollection<TaskItem> Tasks => _tasks;

    public static Project Create(string name, string? description, Guid ownerId) => new()
    {
        Name = name.Trim(),
        Description = NormalizeDescription(description),
        OwnerId = ownerId,
    };

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = NormalizeDescription(description);
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
