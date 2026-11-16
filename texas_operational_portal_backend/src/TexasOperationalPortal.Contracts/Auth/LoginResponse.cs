using System.Collections.Generic;

namespace TexasOperationalPortal.Contracts.Auth;

public sealed record LoginResponse(
    long Id,
    string AccessToken,
    string Name,
    string Email,
    string UserType,
    List<string> Permissions,
    string? AvatarUrl = null);
