using Microsoft.EntityFrameworkCore;

namespace RAG.Models.Entities;

/// <summary>
/// 開放平台（應用程式權杖、暫存區）的資料表設定；由 OnModelCreatingPartial 呼叫
/// </summary>
public partial class WenDaHubContext
{
    public virtual DbSet<ApiToken> ApiToken { get; set; }

    public virtual DbSet<KnowledgeDraft> KnowledgeDraft { get; set; }

    public virtual DbSet<Webhook> Webhook { get; set; }

    public virtual DbSet<WebhookDelivery> WebhookDelivery { get; set; }

    private static void ConfigurePlatform(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");
            entity.HasIndex(e => e.TokenHash, "ux_apitoken_hash").IsUnique();
            entity.HasIndex(e => e.UserID, "ix_apitoken_user");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.UserID).HasColumnType("int(11)");
            entity.Property(e => e.Name).HasMaxLength(50).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.TokenPrefix).HasMaxLength(16);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.Property(e => e.Scopes).HasMaxLength(200);
            entity.Property(e => e.SpaceIDs).HasMaxLength(200);
            entity.Property(e => e.CreateAt).HasColumnType("datetime");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.LastUsedAt).HasColumnType("datetime");
            entity.Property(e => e.RevokedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_apitoken_user");
        });

        modelBuilder.Entity<KnowledgeDraft>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");
            entity.HasIndex(e => new { e.SpaceID, e.UserID, e.Status }, "ix_draft_space_user_status");
            entity.HasIndex(e => new { e.SpaceID, e.UserID, e.ContentHash }, "ix_draft_hash");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.SpaceID).HasColumnType("int(11)");
            entity.Property(e => e.UserID).HasColumnType("int(11)");
            entity.Property(e => e.QaItemTypeID).HasColumnType("int(11)");
            entity.Property(e => e.AcceptedQuestionID).HasColumnType("int(11)");
            entity.Property(e => e.Question).HasColumnType("text").UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Answer).HasColumnType("text").UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Source).HasMaxLength(100).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.SourceRef).HasMaxLength(500).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.ContentHash).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(DraftStatus.Pending);
            entity.Property(e => e.CreateAt).HasColumnType("datetime");
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime");
            entity.Property(e => e.Version).HasMaxLength(32).IsConcurrencyToken();

            entity.HasOne(d => d.Space).WithMany()
                .HasForeignKey(d => d.SpaceID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_draft_space");
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_draft_user");
        });

        modelBuilder.Entity<Webhook>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");
            entity.HasIndex(e => e.UserID, "ix_webhook_user");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.UserID).HasColumnType("int(11)");
            entity.Property(e => e.Name).HasMaxLength(50).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Url).HasMaxLength(500);
            entity.Property(e => e.SecretProtected).HasMaxLength(200);
            entity.Property(e => e.Events).HasMaxLength(200);
            entity.Property(e => e.DisabledReason).HasMaxLength(200).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.CreateAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_webhook_user");
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt }, "ix_delivery_due");
            entity.HasIndex(e => new { e.WebhookID, e.CreateAt }, "ix_delivery_webhook");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.WebhookID).HasColumnType("int(11)");
            entity.Property(e => e.EventID).HasMaxLength(36);
            entity.Property(e => e.Event).HasMaxLength(40);
            entity.Property(e => e.Payload).HasColumnType("text").UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(DeliveryStatus.Pending);
            entity.Property(e => e.LastStatusCode).HasColumnType("int(11)");
            entity.Property(e => e.LastError).HasMaxLength(300).UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.NextAttemptAt).HasColumnType("datetime");
            entity.Property(e => e.CreateAt).HasColumnType("datetime");
            entity.Property(e => e.DeliveredAt).HasColumnType("datetime");

            entity.HasOne(d => d.Webhook).WithMany()
                .HasForeignKey(d => d.WebhookID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_delivery_webhook");
        });
    }
}
