using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Common.Interfaces;

public interface IOperationalDbContext
{
    DbSet<OperationalUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OperationalAssignment> OperationalAssignments { get; }
    DbSet<OperationalAssignmentHistory> OperationalAssignmentHistories { get; }
    DbSet<WorkloadLimit> WorkloadLimits { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
