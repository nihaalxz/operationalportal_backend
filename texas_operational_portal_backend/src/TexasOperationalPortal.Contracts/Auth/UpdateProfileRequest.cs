namespace TexasOperationalPortal.Contracts.Auth;

public sealed record UpdateProfileRequest(string Name, string? AvatarUrl);
