using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Exceptions;
using MediatR;

namespace CqrsPoC.Application.Commands.CompleteOrder;

public sealed class CompleteOrderCommandHandler(
    IOrderRepository repository,
    IEventPublisher publisher
) : IRequestHandler<CompleteOrderCommand>
{
    public async Task Handle(CompleteOrderCommand request, CancellationToken ct)
    {
        var order =
            await repository.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.Complete();

        await repository.UpdateAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        await publisher.PublishAsync(new OrderCompletedEvent(order.Id, order.UpdatedAt!.Value), ct);
    }
}
