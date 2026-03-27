namespace CqrsPoC.Domain.Enums;

public enum OrderState
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Completed = 3,
    Cancelled = 4,
}
