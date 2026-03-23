using MediatR;

namespace CqrsPoC.Application.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest;
