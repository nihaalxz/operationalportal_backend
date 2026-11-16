using System;

namespace TexasOperationalPortal.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public OperationalUser User { get; set; } = null!;
}
