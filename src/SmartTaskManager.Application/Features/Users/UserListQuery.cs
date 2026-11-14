using FluentValidation;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Users;

public static class UserSortFields
{
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string CreatedAt = "createdAt";

    public static readonly IReadOnlyCollection<string> ForList = [Name, Email, Role, CreatedAt];
    public static readonly IReadOnlyCollection<string> ForLookup = [Name, Email];
}

/// <summary>Admin user list. Search matches name and email.</summary>
public sealed class UserListQuery : PagedQuery
{
    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }
}

/// <summary>Active users only. Search matches name and email.</summary>
public sealed class UserLookupQuery : PagedQuery
{
    public const int MaxLookupPageSize = 50;
}

internal sealed class UserListQueryValidator : PagedQueryValidator<UserListQuery>
{
    public UserListQueryValidator()
        : base(UserSortFields.ForList)
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

internal sealed class UserLookupQueryValidator : PagedQueryValidator<UserLookupQuery>
{
    public UserLookupQueryValidator()
        : base(UserSortFields.ForLookup, UserLookupQuery.MaxLookupPageSize)
    {
    }
}
