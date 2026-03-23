using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Exceptions;
using MediatR;

namespace CqrsPoC.Application.Commands.ConfirmOrder;

public sealed class ConfirmOrderCommandHandler(
    IOrderRepository repository,
    IEventPublisher  publisher)
    : IRequestHandler<ConfirmOrderCommand>
{
    public async Task Handle(ConfirmOrderCommand request, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.Confirm();

        await repository.UpdateAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        await publisher.PublishAsync(
            new OrderConfirmedEvent(order.Id, order.UpdatedAt!.Value), ct);
    }
}
