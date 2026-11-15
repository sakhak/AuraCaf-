
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Payments;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _payments;
    private readonly AppDbContext _db;

    public PaymentService(
        IPaymentRepository payments,
        AppDbContext db)
    {
        _payments = payments;
        _db = db;
    }

    public async Task<PaymentResponseDto> CreateCashAsync(
        string userId,
        CreateCashPaymentDto dto)
    {
        if (dto.OrderId <= 0 || dto.CashReceivedCents < 0)
            throw new ArgumentException(
                "Invalid order or cash amount.");

        // SQLite allows only one writer at a time.
        // The transaction groups the payment and order update.
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var staff = await _db.StaffMembers
            .SingleOrDefaultAsync(x => x.UserId == userId);

        if (staff is null ||
            staff.EmploymentStatus != "Active" ||
            staff.Position != "Cashier")
        {
            throw new InvalidOperationException(
                "An active cashier profile is required.");
        }

        var shift = await _db.WorkShifts
            .SingleOrDefaultAsync(x =>
                x.StaffId == staff.Id &&
                x.ClockOutUtc == null);

        if (shift is null)
            throw new InvalidOperationException(
                "Clock in before collecting payment.");

        var order = await _db.Orders
            .SingleOrDefaultAsync(x => x.Id == dto.OrderId);

        if (order is null ||
            order.SoldByStaffId != staff.Id)
        {
            throw new KeyNotFoundException(
                "Order not found.");
        }

        if (order.Status != "Pending")
            throw new InvalidOperationException(
                "This order is not pending payment.");

        if (order.TotalCents < 0)
            throw new InvalidOperationException(
                "Order has an invalid total.");

        if (dto.CashReceivedCents < order.TotalCents)
            throw new ArgumentException(
                "Cash received is less than the order total.");

        var existing = await _payments.GetByOrderIdAsync(
            order.Id);

        if (existing is not null)
            throw new InvalidOperationException(
                "This order already has a payment.");

        long change =
            (long)dto.CashReceivedCents - order.TotalCents;

        if (change > int.MaxValue)
            throw new ArgumentException(
                "Cash amount exceeds the supported limit.");

        var payment = new Payment
        {
            OrderId = order.Id,
            Order = order,

            CollectedByStaffId = staff.Id,
            CollectedByStaff = staff,

            WorkShiftId = shift.Id,

            Method = "Cash",
            Status = "Completed",

            AmountCents = order.TotalCents,
            CashReceivedCents = dto.CashReceivedCents,
            ChangeCents = (int)change,

            PaidAtUtc = DateTime.UtcNow
        };

        order.Status = "Paid";

        await _payments.AddAsync(payment);
        await _payments.SaveChangesAsync();

        await transaction.CommitAsync();

        return ToResponse(payment);
    }

    public async Task<PaymentResponseDto?> GetByIdAsync(int id)
    {
        var payment = await _payments.GetByIdAsync(id);

        return payment is null ? null : ToResponse(payment);
    }

    public async Task<PaymentResponseDto?> GetByOrderIdAsync(
        int orderId,
        string userId,
        bool isAdmin)
    {
        var payment = await _payments.GetByOrderIdAsync(
            orderId);

        if (payment is null)
            return null;

        if (!isAdmin)
        {
            var staff = await _db.StaffMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.UserId == userId);

            if (staff is null ||
                payment.Order.SoldByStaffId != staff.Id)
            {
                return null;
            }
        }

        return ToResponse(payment);
    }

    public async Task<List<PaymentResponseDto>> GetAllAsync()
    {
        var payments = await _payments.GetAllAsync();

        return payments.Select(ToResponse).ToList();
    }

    private static PaymentResponseDto ToResponse(Payment p)
    {
        return new PaymentResponseDto
        {
            Id = p.Id,
            OrderId = p.OrderId,
            OrderNumber = p.Order.OrderNumber,

            CollectedByStaffId = p.CollectedByStaffId,
            CashierName = p.CollectedByStaff.FullName,

            WorkShiftId = p.WorkShiftId,

            Method = p.Method,
            Status = p.Status,

            AmountCents = p.AmountCents,
            CashReceivedCents = p.CashReceivedCents,
            ChangeCents = p.ChangeCents,

            PaidAtUtc = p.PaidAtUtc
        };
    }
}
