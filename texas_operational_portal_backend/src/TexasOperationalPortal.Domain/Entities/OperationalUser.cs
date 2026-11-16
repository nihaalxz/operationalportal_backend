using System;
using System.Collections.Generic;

namespace TexasOperationalPortal.Domain.Entities;

public sealed class OperationalUser
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserType UserType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PermissionsJson { get; set; } // Stores JSON string of permissions (e.g. ["visas", "vfs"])
    public string? AvatarUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
