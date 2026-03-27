using MediatR;

namespace CqrsPoC.Application.Commands.CompleteOrder;

public record CompleteOrderCommand(Guid OrderId) : IRequest;
