using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Exceptions;
using MediatR;

namespace CqrsPoC.Application.Commands.ShipOrder;

public sealed class ShipOrderCommandHandler(
    IOrderRepository repository,
    IEventPublisher  publisher)
    : IRequestHandler<ShipOrderCommand>
{
    public async Task Handle(ShipOrderCommand request, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.Ship();

        await repository.UpdateAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        await publisher.PublishAsync(
            new OrderShippedEvent(order.Id, order.UpdatedAt!.Value), ct);
    }
}
