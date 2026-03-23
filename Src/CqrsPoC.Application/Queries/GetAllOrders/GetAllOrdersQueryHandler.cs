using CqrsPoC.Application.Interfaces;
using CqrsPoC.Application.Queries.GetOrder;
using MediatR;

namespace CqrsPoC.Application.Queries.GetAllOrders;

public sealed class GetAllOrdersQueryHandler(IOrderRepository repository)
    : IRequestHandler<GetAllOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<IReadOnlyList<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken ct)
    {
        var orders = await repository.GetAllAsync(ct);
        return orders.Select(o => new OrderDto(
            o.Id, o.CustomerName, o.ProductName, o.Amount,
            o.State, o.State.ToString(), o.CancelReason,
            o.PermittedTriggers, o.CreatedAt, o.UpdatedAt))
            .ToList();
    }
}
