using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Entities;
using MediatR;

namespace CqrsPoC.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository repository,
    IEventPublisher publisher
) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = Order.Create(request.CustomerName, request.ProductName, request.Amount);

        await repository.AddAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        // Publish integration event via Rebus → RabbitMQ
        await publisher.PublishAsync(
            new OrderCreatedEvent(
                order.Id,
                order.CustomerName,
                order.ProductName,
                order.Amount,
                order.CreatedAt
            ),
            ct
        );

        return order.Id;
    }
}
