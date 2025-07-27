namespace WorkerService.Domain.Entities;

public enum OrderStatus
{
    Pending = 0,
    Validated = 1,
    PaymentProcessing = 2,
    Paid = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6
}