using CqrsPoC.Application.Commands.CancelOrder;
using CqrsPoC.Application.Commands.CompleteOrder;
using CqrsPoC.Application.Commands.ConfirmOrder;
using CqrsPoC.Application.Commands.CreateOrder;
using CqrsPoC.Application.Commands.ShipOrder;
using CqrsPoC.Application.Queries.GetAllOrders;
using CqrsPoC.Application.Queries.GetOrder;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CqrsPoC.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    // ── QUERIES ──────────────────────────────────────────────────────────────

    /// <summary>Returns all orders, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllOrdersQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single order by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ── COMMANDS ─────────────────────────────────────────────────────────────

    /// <summary>Creates a new order (state: Pending).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct
    )
    {
        var id = await mediator.Send(
            new CreateOrderCommand(request.CustomerName, request.ProductName, request.Amount),
            ct
        );

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>Confirms a Pending order → state: Confirmed.</summary>
    [HttpPut("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ConfirmOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Ships a Confirmed order → state: Shipped.</summary>
    [HttpPut("{id:guid}/ship")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ship(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ShipOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Completes a Shipped order → state: Completed.</summary>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CompleteOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Cancels a Pending or Confirmed order → state: Cancelled.</summary>
    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelOrderRequest request,
        CancellationToken ct
    )
    {
        await mediator.Send(new CancelOrderCommand(id, request.Reason), ct);
        return NoContent();
    }
}

// ── Request body records ─────────────────────────────────────────────────────
public record CreateOrderRequest(string CustomerName, string ProductName, decimal Amount);

public record CancelOrderRequest(string Reason);
