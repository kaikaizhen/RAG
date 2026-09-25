using Microsoft.EntityFrameworkCore;

namespace RAG.Models.Entities;

/// <summary>
/// Space Runtime 的資料表設定；獨立於 scaffold 產生的 WenDaHubContext.cs
/// </summary>
public partial class WenDaHubContext
{
    public virtual DbSet<Space> Space { get; set; }

    public virtual DbSet<AccountSpace> AccountSpace { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Space>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.Key, "ux_space_key").IsUnique();

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.Key).HasMaxLength(50);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .UseCollation("utf8mb4_unicode_ci")
                .HasCharSet("utf8mb4");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<AccountSpace>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => new { e.UserID, e.SpaceID }, "ux_accountspace_user_space").IsUnique();
            entity.HasIndex(e => e.SpaceID, "fk_accountspace_space");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.UserID).HasColumnType("int(11)");
            entity.Property(e => e.SpaceID).HasColumnType("int(11)");
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            // 帳號刪除時一併移除其 membership；Space 不可在仍有 membership 時刪除
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_accountspace_user");

            entity.HasOne(d => d.Space).WithMany(p => p.AccountSpace)
                .HasForeignKey(d => d.SpaceID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_accountspace_space");
        });
    }
}
