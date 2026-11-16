using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Infrastructure.Persistence;

public sealed class OperationalDbContext : DbContext, IOperationalDbContext
{
    public OperationalDbContext(DbContextOptions<OperationalDbContext> options) : base(options)
    {
    }

    public DbSet<OperationalUser> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<OperationalAssignment> OperationalAssignments { get; set; } = null!;
    public DbSet<OperationalAssignmentHistory> OperationalAssignmentHistories { get; set; } = null!;
    public DbSet<WorkloadLimit> WorkloadLimits { get; set; } = null!;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationalUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.UserType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.Property(e => e.IsUsed).HasDefaultValue(false);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });

        modelBuilder.Entity<OperationalAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityDocumentNumber).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Assigned");
            entity.Property(e => e.Remarks).HasMaxLength(500);

            // Filtered unique index to guarantee at most ONE active 'Assigned' record per EntityType + EntityId
            entity.HasIndex(e => new { e.EntityType, e.EntityId })
                  .IsUnique()
                  .HasFilter("[Status] = 'Assigned'");

            entity.HasOne(e => e.AssignedToUser)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedToUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CompletedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.CompletedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LastModifiedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.LastModifiedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperationalAssignmentHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(e => e.Assignment)
                  .WithMany()
                  .HasForeignKey(e => e.AssignmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PerformedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.PerformedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OldAssignedUser)
                  .WithMany()
                  .HasForeignKey(e => e.OldAssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.NewAssignedUser)
                  .WithMany()
                  .HasForeignKey(e => e.NewAssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkloadLimit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MaxActiveLimit).HasDefaultValue(3);
        });

        base.OnModelCreating(modelBuilder);
    }
}
