using HealthApp.Application.Abstractions;
using HealthApp.Domain.Entities;
using HealthApp.Domain.Enums;

namespace HealthApp.Application.Services;

public sealed class OutletStaffService(
    ICurrentUser current,
    IUserRepository users) : IOutletStaffService
{
    private static readonly HashSet<UserRole> ManagedRoles =
    [
        UserRole.OutletManager,
        UserRole.KitchenStaff
    ];

    public async Task<IReadOnlyList<OutletStaffDto>> GetAsync()
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        return (await users.GetAllAsync())
            .Where(x => x.OutletId == outletId)
            .Where(x => x.Role is UserRole.OutletAdmin or UserRole.OutletManager or UserRole.KitchenStaff or UserRole.Driver)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(Map)
            .ToList();
    }

    public async Task<OutletStaffDto?> CreateAsync(CreateOutletStaffRequest request)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !ManagedRoles.Contains(role))
            throw new ArgumentException("Staff role must be OutletManager or KitchenStaff.");

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("First name, last name and email are required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(email, outletId) is not null)
            throw new InvalidOperationException("A staff account with this email already exists for this outlet.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = throw new InvalidOperationException("Password service was not supplied.")
        };

        return Map(user);
    }

    public async Task<OutletStaffDto?> UpdateAsync(Guid id, UpdateOutletStaffRequest request)
    {
        if (current.OutletId is not Guid outletId)
            throw new UnauthorizedAccessException("Outlet context is required.");

        var user = await users.FindByIdAsync(id)
            ?? throw new KeyNotFoundException("Staff account not found.");

        if (user.OutletId != outletId)
            throw new UnauthorizedAccessException("Staff account does not belong to this outlet.");

        if (user.Role is UserRole.OutletAdmin or UserRole.Driver)
            throw new InvalidOperationException("This staff account cannot be changed from Team.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !ManagedRoles.Contains(role))
            throw new ArgumentException("Staff role must be OutletManager or KitchenStaff.");

        user.Role = role;
        user.IsActive = request.IsActive;
        await users.UpdateAsync(user);
        return Map(user);
    }

    private static OutletStaffDto Map(User x) =>
        new(x.Id, $"{x.FirstName} {x.LastName}".Trim(), x.Email, x.Role.ToString(), x.IsActive);
}
