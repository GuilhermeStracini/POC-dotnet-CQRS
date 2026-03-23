using MediatR;

namespace CqrsPoC.Application.Commands.CreateOrder;

public record CreateOrderCommand(
    string CustomerName,
    string ProductName,
    decimal Amount) : IRequest<Guid>;
