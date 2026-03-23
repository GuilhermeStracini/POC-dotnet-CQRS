using CqrsPoC.Application.Queries.GetOrder;
using MediatR;

namespace CqrsPoC.Application.Queries.GetAllOrders;

public record GetAllOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;
