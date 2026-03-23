using MediatR;

namespace CqrsPoC.Application.Commands.ShipOrder;

public record ShipOrderCommand(Guid OrderId) : IRequest;
