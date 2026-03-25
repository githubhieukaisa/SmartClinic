using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;

namespace SmartClinic.Models;

public partial class SmartClinicDbContext : DbContext
{
    public SmartClinicDbContext()
    {
    }

    public SmartClinicDbContext(DbContextOptions<SmartClinicDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Medicine> Medicines { get; set; }

    public virtual DbSet<MedicinePrice> MedicinePrices { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<PrescriptionDetail> PrescriptionDetails { get; set; }

    public virtual DbSet<QueueTicket> QueueTickets { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Department> Departments { get; set; }
    public virtual DbSet<Room> Rooms { get; set; }
    public virtual DbSet<DoctorShift> DoctorShifts { get; set; }
    public virtual DbSet<LabTest> LabTests { get; set; }
    public virtual DbSet<LabOrder> LabOrders { get; set; }
    public virtual DbSet<LabOrderDetail> LabOrderDetails { get; set; }
    public virtual DbSet<LabPrice> LabPrices { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<int>("TicketNumberSeq")
                .StartsAt(1)
                .IncrementsBy(1);

        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("graphql", "pg_graphql")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Medicines_pkey");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);
            entity.Property(e => e.Unit).HasMaxLength(20);

            // Computed properties không map vào DB
            entity.Ignore(e => e.IsForSale);
            entity.Ignore(e => e.PhysicalStock);
        });

        modelBuilder.Entity<MedicinePrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MedicinePrices_pkey");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EffectiveFrom)
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Price).HasPrecision(10, 2);

            entity.HasOne(e => e.Medicine)
                .WithMany(m => m.MedicinePrices)
                .HasForeignKey(e => e.MedicineId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("MedicinePrices_MedicineId_fkey");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Patients_pkey");

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Prescriptions_pkey");

            entity.HasIndex(e => e.TicketId, "Prescriptions_TicketId_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status)
                .HasConversion<short>()
                .HasColumnType("smallint")
                .HasDefaultValue(PrescriptionStatus.Pending);
            entity.Property(e => e.TotalAmount)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0");

            entity.HasOne(d => d.Ticket).WithOne(p => p.Prescription)
                .HasForeignKey<Prescription>(d => d.TicketId)
                .HasConstraintName("Prescriptions_TicketId_fkey");
        });

        modelBuilder.Entity<PrescriptionDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PrescriptionDetails_pkey");

            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.UsageInstruction).HasMaxLength(255);

            entity.HasOne(d => d.Medicine).WithMany(p => p.PrescriptionDetails)
                .HasForeignKey(d => d.MedicineId)
                .HasConstraintName("PrescriptionDetails_MedicineId_fkey");

            entity.HasOne(d => d.Prescription).WithMany(p => p.PrescriptionDetails)
                .HasForeignKey(d => d.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("PrescriptionDetails_PrescriptionId_fkey");
        });

        modelBuilder.Entity<QueueTicket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("QueueTickets_pkey");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Doctor).WithMany(p => p.QueueTickets)
                .HasForeignKey(d => d.DoctorId)
                .HasConstraintName("QueueTickets_DoctorId_fkey");

            entity.HasOne(d => d.Patient).WithMany(p => p.QueueTickets)
                .HasForeignKey(d => d.PatientId)
                .HasConstraintName("QueueTickets_PatientId_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("QueueTickets_CreatedBy_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.UpdatedByUser).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("QueueTickets_UpdatedBy_fkey")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users_pkey");

            entity.HasIndex(e => e.Username, "Users_Username_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.RoleMask).HasDefaultValue(0);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<LabTest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LabTests_pkey");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(50);

            entity.HasOne(d => d.DefaultRoom)
                .WithMany()
                .HasForeignKey(d => d.DefaultRoomId)
                .HasConstraintName("LabTests_DefaultRoomId_fkey");
        });

        modelBuilder.Entity<LabOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LabOrders_pkey");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.Property(e => e.Status)
                .HasConversion<short>()
                .HasColumnType("smallint")
                .HasDefaultValue(LabOrderStatus.Pending);

            entity.HasOne(d => d.QueueTicket).WithMany(p => p.LabOrders)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("LabOrders_TicketId_fkey");
        });

        modelBuilder.Entity<LabOrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LabOrderDetails_pkey");
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);

            entity.HasOne(d => d.LabOrder).WithMany(p => p.LabOrderDetails)
                .HasForeignKey(d => d.LabOrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("LabOrderDetails_LabOrderId_fkey");

            entity.HasOne(d => d.LabTest).WithMany(p => p.LabOrderDetails)
                .HasForeignKey(d => d.LabTestId)
                .HasConstraintName("LabOrderDetails_LabTestId_fkey");
        });

        modelBuilder.Entity<DoctorShift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("DoctorShifts_pkey");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorShifts)
                .HasForeignKey(d => d.DoctorId)
                .HasConstraintName("DoctorShifts_DoctorId_fkey");

            entity.HasOne(d => d.Room).WithMany(p => p.DoctorShifts)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("DoctorShifts_RoomId_fkey");
        });

        modelBuilder.Entity<LabPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LabPrices_pkey");

            entity.HasOne(e => e.LabTest)
                .WithMany(t => t.LabPrices)
                .HasForeignKey(e => e.LabTestId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LabPrices_LabTestId_fkey");
        });

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // --- Data Seeding (Chạy 1 lần duy nhất lúc Migration) ---
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 8, Name = "Xét nghiệm & Chẩn đoán hình ảnh", Code = "XN_CDHA" }
        );

        modelBuilder.Entity<Room>().HasData(
            new Room { Id = 9, Name = "Phòng Lấy Máu", Location = "Tầng 2", DepartmentId = 8, Flags = RoomFlags.IsActive | RoomFlags.IsLab },
            new Room { Id = 10, Name = "Phòng Siêu Âm", Location = "Tầng 2", DepartmentId = 8, Flags = RoomFlags.IsActive | RoomFlags.IsLab },
            new Room { Id = 11, Name = "Phòng X-Quang", Location = "Tầng 1", DepartmentId = 8, Flags = RoomFlags.IsActive | RoomFlags.IsLab }
        );

        modelBuilder.Entity<LabTest>().HasData(
            new LabTest { Id = 1, Name = "Tổng phân tích tế bào máu", Unit = "Lần", Description = "Xét nghiệm máu cơ bản", DefaultRoomId = 9 },
            new LabTest { Id = 2, Name = "Đường huyết mao mạch", Unit = "Lần", Description = "Kiểm tra tiểu đường", DefaultRoomId = 9 },
            new LabTest { Id = 3, Name = "Sinh hóa máu (Chức năng Gan/Thận)", Unit = "Lần", Description = "AST, ALT, Creatinin, Ure...", DefaultRoomId = 9 },
            new LabTest { Id = 4, Name = "Siêu âm ổ bụng tổng quát", Unit = "Lần", Description = "Siêu âm màu", DefaultRoomId = 10 },
            new LabTest { Id = 5, Name = "Siêu âm tuyến giáp", Unit = "Lần", Description = "Siêu âm màu", DefaultRoomId = 10 },
            new LabTest { Id = 6, Name = "X-Quang ngực thẳng", Unit = "Lần", Description = "Chụp X-quang phổi", DefaultRoomId = 11 }
        );

        OnModelCreatingPartial(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Lấy tất cả các Entity kế thừa từ BaseEntity đang ở trạng thái "Chuẩn bị thêm mới"
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added);

        // 2. Tính toán giờ Việt Nam chuẩn (Unspecified để PostgreSQL không chửi)
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        var unspecifiedVnTime = DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified);

        // 3. Tự động gán giờ cho tất cả
        foreach (var entry in entries)
        {
            entry.Entity.CreatedAt = unspecifiedVnTime;
        }

        // 4. Cho phép EF Core tiếp tục lưu vào Database
        return base.SaveChangesAsync(cancellationToken);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
