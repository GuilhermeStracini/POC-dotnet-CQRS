using CqrsPoC.Application.Interfaces;
using MediatR;

namespace CqrsPoC.Application.Queries.GetOrder;

public sealed class GetOrderQueryHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return null;

        return new OrderDto(
            order.Id,
            order.CustomerName,
            order.ProductName,
            order.Amount,
            order.State,
            order.State.ToString(),
            order.CancelReason,
            order.PermittedTriggers,
            order.CreatedAt,
            order.UpdatedAt
        );
    }
}
