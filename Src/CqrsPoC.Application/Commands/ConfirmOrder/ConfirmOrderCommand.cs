using MediatR;

namespace CqrsPoC.Application.Commands.ConfirmOrder;

public record ConfirmOrderCommand(Guid OrderId) : IRequest;
