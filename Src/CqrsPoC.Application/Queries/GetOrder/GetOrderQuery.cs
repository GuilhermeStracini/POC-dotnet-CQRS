using MediatR;

namespace CqrsPoC.Application.Queries.GetOrder;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;
