using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Exceptions;
using MediatR;

namespace CqrsPoC.Application.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository repository,
    IEventPublisher publisher
) : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order =
            await repository.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.Cancel(request.Reason);

        await repository.UpdateAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        await publisher.PublishAsync(
            new OrderCancelledEvent(order.Id, request.Reason, order.UpdatedAt!.Value),
            ct
        );
    }
}
