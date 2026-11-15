using AuraCafé.API.Models;
using AuraCafé.API.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Staff> StaffMembers => Set<Staff>();

    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<BakongSandboxIntent> BakongSandboxIntents => Set<BakongSandboxIntent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ShiftReconciliation> ShiftReconciliations => Set<ShiftReconciliation>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();





    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).HasMaxLength(150).IsRequired();
            entity.Property(i => i.NormalizedName).HasMaxLength(150).IsRequired();
            entity.HasIndex(i => i.NormalizedName).IsUnique();
            entity.Property(i => i.Unit).HasMaxLength(3).IsRequired();
            entity.Property(i => i.IsActive).HasDefaultValue(true);
            entity.ToTable("Ingredients", table =>
            {
                table.HasCheckConstraint("CK_Ingredient_Quantities", "QuantityOnHand >= 0 AND MinimumQuantity >= 0");
                table.HasCheckConstraint("CK_Ingredient_Unit", "Unit IN ('g','ml','pcs')");
            });
        });
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.RequestId).IsUnique();
            entity.HasIndex(m => new { m.IngredientId, m.Id });
            entity.Property(m => m.Type).HasMaxLength(20).IsRequired();
            entity.Property(m => m.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(m => m.Ingredient).WithMany().HasForeignKey(m => m.IngredientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.PerformedByUser).WithMany().HasForeignKey(m => m.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("StockMovements", table =>
            {
                table.UseSqlReturningClause(false);
                table.HasCheckConstraint("CK_Movement_Balance", "BalanceBefore >= 0 AND BalanceAfter >= 0 AND QuantityChange <> 0 AND BalanceAfter = BalanceBefore + QuantityChange");
                table.HasCheckConstraint("CK_Movement_Type", "(Type = 'StockIn' AND QuantityChange > 0) OR (Type = 'StockOut' AND QuantityChange < 0) OR Type = 'Adjustment'");
            });
        });
        modelBuilder.Entity<ProductIngredient>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.ProductId, r.IngredientId }).IsUnique();
            entity.HasOne(r => r.Product).WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Ingredient).WithMany().HasForeignKey(r => r.IngredientId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("ProductIngredients", table => table.HasCheckConstraint("CK_Recipe_Quantity", "QuantityRequired > 0"));
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(c => c.Name)
                .IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ImageUrl).HasMaxLength(1000);
            entity.Property(p => p.IsActive).HasDefaultValue(true);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(p => p.Description)
                .HasMaxLength(500);

            entity.Property(p => p.PriceCents)
                .IsRequired();

            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.ToTable("Staff");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.StaffCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasIndex(x => x.StaffCode)
                .IsUnique();

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(30);

            entity.Property(x => x.Position)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.EmploymentStatus)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasIndex(x => x.UserId)
                .IsUnique();

            entity.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<Staff>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkShift>(entity =>
        {
            entity.ToTable("WorkShifts");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // SQLite partial unique index:
            // A staff member may have only one open shift.
            entity.HasIndex(x => x.StaffId)
                .IsUnique()
                .HasFilter("\"ClockOutUtc\" IS NULL");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.OrderNumber)
                .IsRequired()
                .HasMaxLength(40);

            entity.HasIndex(x => x.OrderNumber)
                .IsUnique();

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasOne(x => x.SoldByStaff)
                .WithMany()
                .HasForeignKey(x => x.SoldByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WorkShift)
                .WithMany()
                .HasForeignKey(x => x.WorkShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductName)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShiftReconciliation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.WorkShiftId).IsUnique();
            entity.Property(r => r.Note).HasMaxLength(500);
            entity.HasOne(r => r.WorkShift).WithOne().HasForeignKey<ShiftReconciliation>(r => r.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.SubmittedByStaff).WithMany().HasForeignKey(r => r.SubmittedByStaffId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("ShiftReconciliations", table =>
            {
                table.HasCheckConstraint("CK_Reconciliation_Riel", "(OpeningCashRiel IS NULL AND ExpectedCashRiel IS NULL AND CountedCashRiel IS NULL AND DifferenceCashRiel IS NULL) OR (OpeningCashRiel IS NOT NULL AND ExpectedCashRiel IS NOT NULL AND CountedCashRiel IS NOT NULL AND DifferenceCashRiel IS NOT NULL AND OpeningCashRiel >= 0 AND CountedCashRiel >= 0 AND ExpectedCashRiel = OpeningCashRiel AND DifferenceCashRiel = CountedCashRiel - ExpectedCashRiel)");
                table.HasCheckConstraint("CK_Reconciliation_Counts", "OpeningCashCents >= 0 AND CountedCashCents >= 0 AND RevenueCents >= 0 AND PaidOrders >= 0");
                table.HasCheckConstraint("CK_Reconciliation_Arithmetic", "ExpectedCashCents = OpeningCashCents + RevenueCents AND DifferenceCents = CountedCashCents - ExpectedCashCents AND CashReceivedCents - ChangeCents = RevenueCents");
            });
        });

        modelBuilder.Entity<BakongSandboxIntent>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.OrderId).IsUnique();
            entity.HasIndex(i => i.Md5).IsUnique();
            entity.HasIndex(i => i.TransactionHash).IsUnique().HasFilter("\"TransactionHash\" IS NOT NULL");
            entity.Property(i => i.ReceiverAccountId).HasMaxLength(64).IsRequired();
            entity.Property(i => i.QrPayload).HasMaxLength(1024).IsRequired();
            entity.Property(i => i.Md5).HasMaxLength(32).IsRequired();
            entity.Property(i => i.TransactionHash).HasMaxLength(64);
            entity.Property(i => i.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(i => i.Order).WithOne().HasForeignKey<BakongSandboxIntent>(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("BakongSandboxIntents", table =>
            {
                table.HasCheckConstraint("CK_BakongSandbox_Amount", "AmountCents > 0");
                table.HasCheckConstraint("CK_BakongSandbox_Status", "Status IN ('Pending','Expired','TestVerified')");
                table.HasCheckConstraint("CK_BakongSandbox_Verification", "(Status = 'TestVerified' AND TransactionHash IS NOT NULL AND VerifiedAtUtc IS NOT NULL) OR (Status <> 'TestVerified' AND TransactionHash IS NULL AND VerifiedAtUtc IS NULL)");
            });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasIndex(p => p.PaidAtUtc);
            entity.HasIndex(p => new { p.CollectedByStaffId, p.PaidAtUtc });

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.OrderId)
                .IsUnique();

            entity.Property(x => x.Method)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(30);

            entity.HasOne(x => x.Order)
                .WithOne()
                .HasForeignKey<Payment>(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CollectedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CollectedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WorkShift)
                .WithMany()
                .HasForeignKey(x => x.WorkShiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });



    }
}
