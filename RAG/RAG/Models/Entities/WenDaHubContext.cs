using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RAG.Models.Entities;

public partial class WenDaHubContext : DbContext
{
    public WenDaHubContext(DbContextOptions<WenDaHubContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatHeader> ChatHeader { get; set; }

    public virtual DbSet<ChatLog> ChatLog { get; set; }

    public virtual DbSet<QaItemType> QaItemType { get; set; }

    public virtual DbSet<QaType> QaType { get; set; }

    public virtual DbSet<Questions> Questions { get; set; }

    public virtual DbSet<UserCache> UserCache { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("latin1_swedish_ci");

        modelBuilder.Entity<ChatHeader>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.UserID, "fk_user");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.UpdateAt).HasColumnType("datetime");
            entity.Property(e => e.UserID).HasColumnType("int(11)");

            entity.HasOne(d => d.User).WithMany(p => p.ChatHeader)
                .HasForeignKey(d => d.UserID)
                .HasConstraintName("fk_user");
        });

        modelBuilder.Entity<ChatLog>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.ChatHeaderID, "fk_chat");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.Answer).HasColumnType("text");
            entity.Property(e => e.ChatHeaderID).HasColumnType("int(11)");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Questions).HasColumnType("text");

            entity.HasOne(d => d.ChatHeader).WithMany(p => p.ChatLog)
                .HasForeignKey(d => d.ChatHeaderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_chat");
        });

        modelBuilder.Entity<QaItemType>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.QaTypeID, "fk_qatype");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.NameEn).HasMaxLength(20);
            entity.Property(e => e.NameOther).HasMaxLength(20);
            entity.Property(e => e.NameZh).HasMaxLength(20);
            entity.Property(e => e.QaTypeID).HasColumnType("int(11)");

            entity.HasOne(d => d.QaType).WithMany(p => p.QaItemType)
                .HasForeignKey(d => d.QaTypeID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_qatype");
        });

        modelBuilder.Entity<QaType>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.NameEn).HasMaxLength(20);
            entity.Property(e => e.NameOther).HasMaxLength(20);
            entity.Property(e => e.NameZh)
                .HasMaxLength(20)
                .UseCollation("utf8mb4_general_ci");
        });

        modelBuilder.Entity<Questions>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.QaItemTypeID, "fk_questiontype");

            entity.HasIndex(e => e.UserID, "fk_questionuser");

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.Answer)
                .HasColumnType("text")
                .UseCollation("utf8mb4_general_ci");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.QaItemTypeID).HasColumnType("int(11)");
            entity.Property(e => e.Question)
                .HasColumnType("text")
                .UseCollation("utf8mb4_general_ci");
            entity.Property(e => e.UpdateAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.UserID).HasColumnType("int(11)");
            entity.Property(e => e.VectorID).HasMaxLength(100);

            entity.HasOne(d => d.QaItemType).WithMany(p => p.Questions)
                .HasForeignKey(d => d.QaItemTypeID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_questiontype");

            entity.HasOne(d => d.User).WithMany(p => p.Questions)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_questionuser");
        });

        modelBuilder.Entity<UserCache>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PRIMARY");

            entity.HasIndex(e => e.Account, "Account").IsUnique();

            entity.Property(e => e.ID).HasColumnType("int(11)");
            entity.Property(e => e.Account).HasMaxLength(12);
            entity.Property(e => e.Manage).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(50);
            entity.Property(e => e.NameOther).HasMaxLength(50);
            entity.Property(e => e.NameZh)
                .HasMaxLength(50)
                .UseCollation("utf8mb4_unicode_ci");
            entity.Property(e => e.Password).HasMaxLength(12);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
