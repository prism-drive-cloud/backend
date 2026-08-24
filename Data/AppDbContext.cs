using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<FileEntity> Files { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureBaseEntity(modelBuilder);
            ConfigureTenant(modelBuilder);
            ConfigureUser(modelBuilder);
            ConfigureFolder(modelBuilder);
            ConfigureFile(modelBuilder);
        }

        private static void ConfigureBaseEntity(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("Id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    modelBuilder.Entity(entityType.ClrType)
                        .Property("CreatedAt")
                        .HasDefaultValueSql("now()")
                        .ValueGeneratedOnAdd();

                    modelBuilder.Entity(entityType.ClrType)
                        .Property("UpdatedAt")
                        .HasDefaultValueSql("now()")
                        .ValueGeneratedOnAddOrUpdate();
                }
            }
        }

        private static void ConfigureTenant(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("tenants");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
                entity.Property(e => e.Slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
                entity.Property(e => e.IsPersonal).HasColumnName("is_personal").HasDefaultValue(false);
                entity.Property(e => e.StorageQuotaBytes).HasColumnName("storage_quota_bytes").HasDefaultValue(1073741824L);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("uq_tenants_slug");
            });
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
                entity.Property(e => e.FullName).HasColumnName("full_name").IsRequired().HasMaxLength(255);
                entity.Property(e => e.Role).HasColumnName("role").IsRequired().HasConversion<string>();
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("uq_users_email");
                entity.HasIndex(e => e.TenantId).HasDatabaseName("idx_users_tenant_id");
            });
        }

        private static void ConfigureFolder(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Folder>(entity =>
            {
                entity.ToTable("folders");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.OwnerId).HasColumnName("owner_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.TenantId).HasDatabaseName("idx_folders_tenant_id");
                entity.HasIndex(e => e.OwnerId).HasDatabaseName("idx_folders_owner_id");

                entity.HasOne<Tenant>()
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_folders_tenant_id");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("fk_folders_owner_id");
            });
        }

        private static void ConfigureFile(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileEntity>(entity =>
            {
                entity.ToTable("files");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.OwnerId).HasColumnName("owner_id").IsRequired();
                entity.Property(e => e.FolderId).HasColumnName("folder_id");
                entity.Property(e => e.OriginalName).HasColumnName("original_name").IsRequired().HasMaxLength(255);
                entity.Property(e => e.MimeType).HasColumnName("mime_type").IsRequired().HasMaxLength(100);
                entity.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
                entity.Property(e => e.S3Key).HasColumnName("s3_key").IsRequired().HasMaxLength(500);
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.TenantId).HasDatabaseName("idx_files_tenant_id");
                entity.HasIndex(e => e.OwnerId).HasDatabaseName("idx_files_owner_id");
                entity.HasIndex(e => e.FolderId).HasDatabaseName("idx_files_folder_id");
                entity.HasIndex(e => e.S3Key).IsUnique().HasDatabaseName("uq_files_s3_key");

                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne<Tenant>()
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_files_tenant_id");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("fk_files_owner_id");

                entity.HasOne<Folder>()
                    .WithMany()
                    .HasForeignKey(e => e.FolderId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("fk_files_folder_id");
            });
        }
    }
}