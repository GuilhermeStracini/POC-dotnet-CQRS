using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CqrsPoC.Application.Queries.GetOrder;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Enums;
using CqrsPoC.Tests.E2E.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace CqrsPoC.Tests.E2E.Endpoints;

/// <summary>
/// End-to-end tests that exercise the full HTTP stack:
///   Client → Controller → MediatR pipeline → Domain → Repository (InMemory)
///
/// All tests share a single <see cref="OrdersWebApplicationFactory"/> to avoid
/// the startup cost of booting the application per test. Each test that needs
/// isolation seeds its own data.
/// </summary>
public sealed class OrdersApiTests : IClassFixture<OrdersWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly OrdersWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OrdersApiTests(OrdersWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // POST /api/orders  — Create
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task POST_CreateOrder_Returns201WithLocationAndId()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerName = "E2E Alice",
                productName = "E2E Widget",
                amount = 149.99,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_CreateOrder_PublishesOrderCreatedEvent()
    {
        _factory.PublisherMock.Invocations.Clear();

        await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerName = "Event Tester",
                productName = "Event Product",
                amount = 50.00,
            }
        );

        _factory.PublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<OrderCreatedEvent>(), CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task POST_CreateOrder_WithZeroAmount_Returns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerName = "Bad Request",
                productName = "Product",
                amount = 0,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().Contain("Exception");
    }

    [Fact]
    public async Task POST_CreateOrder_WithEmptyCustomerName_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerName = "",
                productName = "Product",
                amount = 50,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /api/orders  — List
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GET_AllOrders_Returns200WithListOfOrders()
    {
        await _factory.SeedOrderAsync("List A", "Prod", 10m);
        await _factory.SeedOrderAsync("List B", "Prod", 20m);

        var response = await _client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<OrderDto>>(JsonOpts);
        items.Should().NotBeNullOrEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /api/orders/{id}  — Get by ID
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GET_OrderById_ExistingOrder_Returns200WithOrderDto()
    {
        var id = await _factory.SeedOrderAsync("Get Test", "Widget", 55m);

        var response = await _client.GetAsync($"/api/orders/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOpts);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(id);
        dto.CustomerName.Should().Be("Get Test");
        dto.State.Should().Be(OrderState.Pending);
        dto.PermittedTriggers.Should().Contain("Confirm");
    }

    [Fact]
    public async Task GET_OrderById_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_OrderById_InvalidGuid_Returns400OrNotFound()
    {
        var response = await _client.GetAsync("/api/orders/not-a-guid");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUT /api/orders/{id}/confirm
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PUT_Confirm_PendingOrder_Returns204AndStateIsConfirmed()
    {
        var id = await _factory.SeedOrderAsync("Confirm Test", "Prod", 10m);

        var response = await _client.PutAsync($"/api/orders/{id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await GetOrderAsync(id);
        dto!.State.Should().Be(OrderState.Confirmed);
        dto.PermittedTriggers.Should().Contain("Ship");
    }

    [Fact]
    public async Task PUT_Confirm_NonExistentOrder_Returns404()
    {
        var response = await _client.PutAsync($"/api/orders/{Guid.NewGuid()}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_Confirm_AlreadyConfirmedOrder_Returns400DomainException()
    {
        var id = await _factory.SeedOrderAsync();
        await _client.PutAsync($"/api/orders/{id}/confirm", null);

        // Second confirm — invalid transition
        var response = await _client.PutAsync($"/api/orders/{id}/confirm", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUT /api/orders/{id}/ship
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PUT_Ship_ConfirmedOrder_Returns204AndStateIsShipped()
    {
        var id = await _factory.SeedOrderAsync("Ship Test", "Prod", 10m);
        await _client.PutAsync($"/api/orders/{id}/confirm", null);

        var response = await _client.PutAsync($"/api/orders/{id}/ship", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetOrderAsync(id))!.State.Should().Be(OrderState.Shipped);
    }

    [Fact]
    public async Task PUT_Ship_PendingOrder_Returns400DomainException()
    {
        var id = await _factory.SeedOrderAsync();

        var response = await _client.PutAsync($"/api/orders/{id}/ship", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUT /api/orders/{id}/complete
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PUT_Complete_ShippedOrder_Returns204AndStateIsCompleted()
    {
        var id = await _factory.SeedOrderAsync("Complete Test", "Prod", 10m);
        await _client.PutAsync($"/api/orders/{id}/confirm", null);
        await _client.PutAsync($"/api/orders/{id}/ship", null);

        var response = await _client.PutAsync($"/api/orders/{id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await GetOrderAsync(id);
        dto!.State.Should().Be(OrderState.Completed);
        dto.PermittedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task PUT_Complete_ConfirmedOrder_Returns400DomainException()
    {
        var id = await _factory.SeedOrderAsync();
        await _client.PutAsync($"/api/orders/{id}/confirm", null);

        var response = await _client.PutAsync($"/api/orders/{id}/complete", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUT /api/orders/{id}/cancel
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PUT_Cancel_PendingOrder_Returns204AndStateIsCancelled()
    {
        var id = await _factory.SeedOrderAsync("Cancel Test", "Prod", 10m);

        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{id}/cancel",
            new { reason = "Changed mind" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await GetOrderAsync(id);
        dto!.State.Should().Be(OrderState.Cancelled);
        dto.CancelReason.Should().Be("Changed mind");
        dto.PermittedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task PUT_Cancel_ConfirmedOrder_Returns204()
    {
        var id = await _factory.SeedOrderAsync();
        await _client.PutAsync($"/api/orders/{id}/confirm", null);

        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{id}/cancel",
            new { reason = "Out of stock" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PUT_Cancel_ShippedOrder_Returns400DomainException()
    {
        var id = await _factory.SeedOrderAsync();
        await _client.PutAsync($"/api/orders/{id}/confirm", null);
        await _client.PutAsync($"/api/orders/{id}/ship", null);

        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{id}/cancel",
            new { reason = "Too late" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_Cancel_NonExistentOrder_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/orders/{Guid.NewGuid()}/cancel",
            new { reason = "reason" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Full happy-path E2E scenario
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FullE2ELifecycle_CreateConfirmShipComplete_AllStatesCorrect()
    {
        // 1. Create
        var createResponse = await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerName = "E2E Full Test",
                productName = "E2E Product",
                amount = 299.99,
            }
        );
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        // 2. Verify Pending
        var pending = await GetOrderAsync(id);
        pending!.State.Should().Be(OrderState.Pending);
        pending.PermittedTriggers.Should().Contain("Confirm").And.Contain("Cancel");

        // 3. Confirm
        (await _client.PutAsync($"/api/orders/{id}/confirm", null))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        var confirmed = await GetOrderAsync(id);
        confirmed!.State.Should().Be(OrderState.Confirmed);
        confirmed.PermittedTriggers.Should().Contain("Ship").And.Contain("Cancel");

        // 4. Ship
        (await _client.PutAsync($"/api/orders/{id}/ship", null))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        var shipped = await GetOrderAsync(id);
        shipped!.State.Should().Be(OrderState.Shipped);
        shipped.PermittedTriggers.Should().ContainSingle().Which.Should().Be("Complete");

        // 5. Complete
        (await _client.PutAsync($"/api/orders/{id}/complete", null))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        var completed = await GetOrderAsync(id);
        completed!.State.Should().Be(OrderState.Completed);
        completed.PermittedTriggers.Should().BeEmpty();
        completed.UpdatedAt.Should().NotBeNull();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Problem Details validation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task InvalidTransition_ResponseBodyContainsProblemDetails()
    {
        var id = await _factory.SeedOrderAsync();

        var response = await _client.PutAsync($"/api/orders/{id}/ship", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<OrderDto?> GetOrderAsync(Guid id) =>
        await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{id}", JsonOpts);
}
