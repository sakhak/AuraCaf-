
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Orders;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly AppDbContext _db;

    public OrderService(
        IOrderRepository orders,
        AppDbContext db)
    {
        _orders = orders;
        _db = db;
    }

    public async Task<OrderResponseDto> CreateAsync(
        string userId,
        CreateOrderDto dto)
    {
        if (dto.Items is null ||
            dto.Items.Count == 0 ||
            dto.Items.Count > 50)
        {
            throw new ArgumentException(
                "Order must contain 1 to 50 items.");
        }

        if (dto.Items.Any(x =>
            x.ProductId <= 0 ||
            x.Quantity < 1 ||
            x.Quantity > 100))
        {
            throw new ArgumentException(
                "Invalid product or quantity.");
        }

        // Keep each product on one order line.
        if (dto.Items.Select(x => x.ProductId)
            .Distinct().Count() != dto.Items.Count)
        {
            throw new ArgumentException(
                "Duplicate products are not allowed.");
        }

        // Begin transaction before reading shift
        // and product data used for this order.
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
                "Clock in before creating an order.");

        var productIds = dto.Items
            .Select(x => x.ProductId)
            .ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var order = new Order
        {
            OrderNumber = "ORD-" +
                Guid.NewGuid().ToString("N")[..16]
                    .ToUpperInvariant(),

            SoldByStaffId = staff.Id,
            WorkShiftId = shift.Id,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        long total = 0;

        foreach (var requestItem in dto.Items)
        {
            if (!products.TryGetValue(
                requestItem.ProductId, out var product))
            {
                throw new ArgumentException(
                    "One or more products do not exist.");
            }

            if (!product.IsAvailable || !product.IsActive)
                throw new ArgumentException(
                    $"Product {product.Name} is unavailable.");

            if (product.PriceCents < 0)
                throw new InvalidOperationException(
                    "Product has an invalid price.");

            long subtotal = checked(
                (long)product.PriceCents *
                requestItem.Quantity);

            total = checked(total + subtotal);

            if (subtotal > int.MaxValue ||
                total > int.MaxValue)
            {
                throw new ArgumentException(
                    "Order amount exceeds the supported limit.");
            }

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = requestItem.Quantity,
                UnitPriceCents = product.PriceCents,
                SubtotalCents = (int)subtotal
            });
        }

        order.TotalCents = (int)total;

        await _orders.AddAsync(order);
        await _orders.SaveChangesAsync();

        await transaction.CommitAsync();

        // The staff entity was loaded earlier.
        order.SoldByStaff = staff;

        return ToResponse(order);
    }

    public async Task<OrderResponseDto?> GetByIdAsync(
        int id,
        string userId,
        bool isAdmin)
    {
        var order = await _orders.GetByIdAsync(id);

        if (order is null)
            return null;

        if (!isAdmin)
        {
            var staff = await _db.StaffMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.UserId == userId);

            if (staff is null ||
                order.SoldByStaffId != staff.Id)
            {
                return null;
            }
        }

        return ToResponse(order);
    }

    public async Task<List<OrderResponseDto>>
        GetMyOrdersAsync(string userId)
    {
        var staff = await _db.StaffMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.UserId == userId);

        if (staff is null)
            return new List<OrderResponseDto>();

        var orders = await _orders.GetByStaffAsync(
            staff.Id);

        return orders.Select(ToResponse).ToList();
    }

    public async Task<List<OrderResponseDto>> GetAllAsync()
    {
        var orders = await _orders.GetAllAsync();

        return orders.Select(ToResponse).ToList();
    }

    private static OrderResponseDto ToResponse(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SoldByStaffId = order.SoldByStaffId,
            CashierName = order.SoldByStaff.FullName,
            WorkShiftId = order.WorkShiftId,
            TotalCents = order.TotalCents,
            Status = order.Status,
            CreatedAtUtc = order.CreatedAtUtc,

            Items = order.Items.Select(x =>
                new OrderItemResponseDto
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    Quantity = x.Quantity,
                    UnitPriceCents = x.UnitPriceCents,
                    SubtotalCents = x.SubtotalCents
                }).ToList()
        };
    }
}
