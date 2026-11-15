namespace AuraCafé.Web.Models;

public class SalesReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string TimeZone { get; set; } = "Asia/Bangkok";
    public DateTime GeneratedAtUtc { get; set; }
    public long RevenueCents { get; set; }
    public int PaidOrders { get; set; }
    public long CashReceivedCents { get; set; }
    public long ChangeCents { get; set; }
    public long NetCashCents => CashReceivedCents - ChangeCents;
    public int PendingOrders { get; set; }
    public int OrdersCreated { get; set; }
    public List<SalesPeriodDto> Daily { get; set; } = [];
    public List<SalesPeriodDto> Monthly { get; set; } = [];
    public List<CashierSalesDto> Cashiers { get; set; } = [];
    public List<ProductSalesDto> Products { get; set; } = [];
}

public class SalesPeriodDto
{
    public DateTime Date { get; set; }
    public long RevenueCents { get; set; }
    public int PaidOrders { get; set; }
}

public class CashierSalesDto
{
    public int StaffId { get; set; }
    public string Name { get; set; } = "";
    public int PaidOrders { get; set; }
    public long RevenueCents { get; set; }
    public long CashReceivedCents { get; set; }
    public long ChangeCents { get; set; }
}

public class ProductSalesDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public long Quantity { get; set; }
    public long RevenueCents { get; set; }
}
