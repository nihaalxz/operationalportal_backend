using System.Collections.Generic;

namespace TexasOperationalPortal.Contracts.Staff;

public sealed record StaffSyncRequest(
    string Email,
    string Name,
    string UserType,
    List<string>? Permissions,
    string? PasswordHash,
    bool IsActive
);
