using System;

namespace TexasOperationalPortal.Domain.Entities;

public class WorkloadLimit
{
    public long Id { get; set; }
    public string EntityType { get; set; } = null!; // VISA, VFS, HOLIDAY
    public int MaxActiveLimit { get; set; } = 3;
    public long? UserId { get; set; } // Null for global default, or specific operator UserId
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedByUserId { get; set; }
}
