using CqrsPoC.Domain.Enums;

namespace CqrsPoC.Application.Queries.GetOrder;

public record OrderDto(
    Guid      Id,
    string    CustomerName,
    string    ProductName,
    decimal   Amount,
    OrderState State,
    string    StateName,
    string?   CancelReason,
    IEnumerable<string> PermittedTriggers,
    DateTime  CreatedAt,
    DateTime? UpdatedAt);
