# PoC .NET - CQRS

[![wakatime](https://wakatime.com/badge/github/GuilhermeStracini/POC-dotnet-CQRS.svg)](https://wakatime.com/badge/github/GuilhermeStracini/POC-dotnet-CQRS)
[![Maintainability](https://api.codeclimate.com/v1/badges/cc989f187ec5a1a8ced8/maintainability)](https://codeclimate.com/github/GuilhermeStracini/POC-dotnet-CQRS/maintainability)
[![Test Coverage](https://api.codeclimate.com/v1/badges/cc989f187ec5a1a8ced8/test_coverage)](https://codeclimate.com/github/GuilhermeStracini/POC-dotnet-CQRS/test_coverage)
[![CodeFactor](https://www.codefactor.io/repository/github/GuilhermeStracini/POC-dotnet-CQRS/badge)](https://www.codefactor.io/repository/github/GuilhermeStracini/POC-dotnet-CQRS)
[![GitHub license](https://img.shields.io/github/license/GuilhermeStracini/POC-dotnet-CQRS)](https://github.com/GuilhermeStracini/POC-dotnet-CQRS)
[![GitHub last commit](https://img.shields.io/github/last-commit/GuilhermeStracini/POC-dotnet-CQRS)](https://github.com/GuilhermeStracini/POC-dotnet-CQRS)

🔬 Proof of Concept of CQRS pattern in .NET using RabbitMQ, ReBus, State Machine, MediatR and Docker

> **Proof of Concept** demonstrating the **CQRS pattern** in **.NET 10** using
> MediatR, Rebus, RabbitMQ, Stateless State Machine, EF Core, and Docker.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Order Lifecycle — State Machine](#order-lifecycle--state-machine)
- [CQRS Flow](#cqrs-flow)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Run with Docker Compose](#run-with-docker-compose)
  - [Run Locally (without Docker)](#run-locally-without-docker)
- [API Reference](#api-reference)
- [Design Decisions](#design-decisions)

---

## Architecture Overview

```
┌────────────────────────────────────────────────────────────────┐
│                        HTTP Client                             │
└───────────────────────────┬────────────────────────────────────┘
                            │  REST
┌───────────────────────────▼────────────────────────────────────┐
│                     CqrsPoC.API                                │
│  OrdersController  →  IMediator                                │
└────────┬──────────────────┬───────────────────────────────────-┘
         │ Commands         │ Queries
┌────────▼──────────────────▼────────────────────────────────────┐
│                  CqrsPoC.Application                           │
│                                                                │
│  ┌─────────────────────┐   ┌─────────────────────────────┐    │
│  │   Command Handlers  │   │      Query Handlers          │    │
│  │  CreateOrder        │   │  GetOrderQueryHandler        │    │
│  │  ConfirmOrder       │   │  GetAllOrdersQueryHandler    │    │
│  │  ShipOrder          │   └──────────────┬──────────────┘    │
│  │  CompleteOrder      │                  │                    │
│  │  CancelOrder        │   ┌──────────────▼──────────────┐    │
│  └────────┬────────────┘   │   IOrderRepository (read)   │    │
│           │                └─────────────────────────────┘    │
│           │ IEventPublisher                                    │
│  ┌────────▼────────────────────────────────────────────────┐  │
│  │              LoggingBehavior (pipeline)                  │  │
│  └─────────────────────────────────────────────────────────┘  │
└────────┬───────────────────────────────────────────────────────┘
         │
┌────────▼───────────────────────────────────────────────────────┐
│                  CqrsPoC.Infrastructure                        │
│                                                                │
│  ┌──────────────────────┐   ┌───────────────────────────────┐ │
│  │   AppDbContext       │   │     RebusEventPublisher       │ │
│  │   (EF Core InMemory) │   │     → IBus (Rebus)            │ │
│  │   OrderRepository    │   │     → RabbitMQ exchange       │ │
│  └──────────────────────┘   └──────────────┬────────────────┘ │
│                                            │ publishes        │
│                             ┌──────────────▼────────────────┐ │
│                             │  Event Handlers (Rebus subs)  │ │
│                             │  OrderCreatedEventHandler     │ │
│                             │  OrderConfirmedEventHandler   │ │
│                             │  OrderShippedEventHandler     │ │
│                             │  OrderCompletedEventHandler   │ │
│                             │  OrderCancelledEventHandler   │ │
│                             └───────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
         │
┌────────▼───────────────────────────────────────────────────────┐
│                      CqrsPoC.Domain                           │
│                                                                │
│  Order (Aggregate Root)                                        │
│    └─ Stateless State Machine                                  │
│         Pending → Confirmed → Shipped → Completed              │
│         Pending/Confirmed → Cancelled                          │
└────────────────────────────────────────────────────────────────┘
```

---

## Tech Stack

| Concern              | Library / Tool                     | Version   |
|---------------------|------------------------------------|-----------|
| Framework            | .NET / ASP.NET Core                | **10.0**  |
| CQRS Mediator        | **MediatR**                        | 12.x      |
| Message Bus          | **Rebus**                          | 8.x       |
| Message Transport    | **Rebus.RabbitMq** (RabbitMQ)      | 10.x      |
| State Machine        | **Stateless**                      | 5.x       |
| ORM / Persistence    | EF Core (InMemory for PoC)         | 10.0      |
| API Docs             | Swashbuckle / Swagger              | 7.x       |
| Containerisation     | Docker + Docker Compose            | —         |

---

## Project Structure

```
CqrsPoC/
├── CqrsPoC.sln
├── Dockerfile
├── docker-compose.yml
└── src/
    ├── CqrsPoC.Contracts/          # Shared integration event records
    │   └── Events/
    │       ├── OrderCreatedEvent.cs
    │       ├── OrderConfirmedEvent.cs
    │       ├── OrderShippedEvent.cs
    │       ├── OrderCompletedEvent.cs
    │       └── OrderCancelledEvent.cs
    │
    ├── CqrsPoC.Domain/             # Pure domain — no framework deps
    │   ├── Entities/
    │   │   └── Order.cs            ← Aggregate root + state machine
    │   ├── Enums/
    │   │   ├── OrderState.cs
    │   │   └── OrderTrigger.cs
    │   └── Exceptions/
    │       ├── DomainException.cs
    │       └── OrderNotFoundException.cs
    │
    ├── CqrsPoC.Application/        # Use-cases, CQRS handlers
    │   ├── Commands/
    │   │   ├── CreateOrder/
    │   │   ├── ConfirmOrder/
    │   │   ├── ShipOrder/
    │   │   ├── CompleteOrder/
    │   │   └── CancelOrder/
    │   ├── Queries/
    │   │   ├── GetOrder/
    │   │   └── GetAllOrders/
    │   ├── Behaviors/
    │   │   └── LoggingBehavior.cs  ← MediatR pipeline (cross-cutting)
    │   ├── Interfaces/
    │   │   ├── IOrderRepository.cs
    │   │   └── IEventPublisher.cs
    │   └── DependencyInjection.cs
    │
    ├── CqrsPoC.Infrastructure/     # EF Core + Rebus implementations
    │   ├── Persistence/
    │   │   ├── AppDbContext.cs
    │   │   └── Repositories/
    │   │       └── OrderRepository.cs
    │   ├── Messaging/
    │   │   ├── RebusEventPublisher.cs
    │   │   └── Handlers/
    │   │       ├── OrderCreatedEventHandler.cs
    │   │       ├── OrderConfirmedEventHandler.cs
    │   │       ├── OrderShippedEventHandler.cs
    │   │       ├── OrderCompletedEventHandler.cs
    │   │       └── OrderCancelledEventHandler.cs
    │   └── DependencyInjection.cs
    │
    └── CqrsPoC.API/                # HTTP entry point
        ├── Controllers/
        │   └── OrdersController.cs
        ├── Program.cs
        ├── appsettings.json
        └── appsettings.Development.json
```

---

## Order Lifecycle — State Machine

The `Order` aggregate embeds a **Stateless** state machine that enforces
all valid lifecycle transitions at the domain level. Invalid transitions
throw a `DomainException`, which the API maps to `400 Bad Request`.

```
                    ┌─────────┐
                    │ Pending │ ◄─── Initial state on creation
                    └────┬────┘
          [confirm]      │       [cancel]
              ┌──────────┘──────────────┐
              ▼                         ▼
       ┌───────────┐             ┌───────────┐
       │ Confirmed │             │ Cancelled │ (terminal)
       └─────┬─────┘             └───────────┘
    [ship]   │       [cancel]         ▲
             │────────────────────────┘
             ▼
         ┌────────┐
         │Shipped │
         └────┬───┘
   [complete] │
              ▼
        ┌───────────┐
        │ Completed │ (terminal)
        └───────────┘
```

Each `OrderDto` response includes a **`permittedTriggers`** array so clients
always know which transitions are available in the current state.

---

## CQRS Flow

### Command flow (write side)

```
HTTP PUT /api/orders/{id}/confirm
  └─► OrdersController.Confirm()
        └─► IMediator.Send(ConfirmOrderCommand)
              └─► LoggingBehavior (pipeline)
                    └─► ConfirmOrderCommandHandler
                          ├─► IOrderRepository.GetByIdAsync()
                          ├─► order.Confirm()          ← state machine fires
                          ├─► IOrderRepository.SaveChangesAsync()
                          └─► IEventPublisher.PublishAsync(OrderConfirmedEvent)
                                └─► Rebus IBus.Publish()
                                      └─► RabbitMQ exchange
                                            └─► OrderConfirmedEventHandler (subscriber)
```

### Query flow (read side)

```
HTTP GET /api/orders/{id}
  └─► OrdersController.GetById()
        └─► IMediator.Send(GetOrderQuery)
              └─► LoggingBehavior (pipeline)
                    └─► GetOrderQueryHandler
                          └─► IOrderRepository.GetByIdAsync()
                                └─► OrderDto (projection)
```

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker + Compose plugin)
- **.NET 10 SDK** — only needed for local development without Docker

---

### Run with Docker Compose

```bash
# Clone / navigate to the repo root
git clone <repo-url>
cd CqrsPoC

# Build and start both services (RabbitMQ + API)
docker compose up --build

# API Swagger UI  →  http://localhost:8080
# RabbitMQ UI     →  http://localhost:15672  (guest / guest)
```

To stop and remove containers:

```bash
docker compose down -v
```

---

### Run Locally (without Docker)

1. **Start RabbitMQ** via Docker:

```bash
docker run -d \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3.13-management-alpine
```

2. **Run the API:**

```bash
cd src/CqrsPoC.API
dotnet run
# Swagger UI → https://localhost:5001  (or check the console output)
```

---

## API Reference

| Method | Endpoint                        | Description                          | Transition             |
|--------|---------------------------------|--------------------------------------|------------------------|
| `GET`  | `/api/orders`                   | List all orders                      | —                      |
| `GET`  | `/api/orders/{id}`              | Get a single order                   | —                      |
| `POST` | `/api/orders`                   | Create a new order                   | → **Pending**          |
| `PUT`  | `/api/orders/{id}/confirm`      | Confirm a pending order              | Pending → **Confirmed**|
| `PUT`  | `/api/orders/{id}/ship`         | Ship a confirmed order               | Confirmed → **Shipped**|
| `PUT`  | `/api/orders/{id}/complete`     | Complete a shipped order             | Shipped → **Completed**|
| `PUT`  | `/api/orders/{id}/cancel`       | Cancel a pending/confirmed order     | → **Cancelled**        |

### Example: Create Order

```bash
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Guilherme",
    "productName": "Mechanical Keyboard",
    "amount": 149.99
  }'
# → 201 Created  { "id": "3fa85f64-..." }
```

### Example: Full lifecycle

```bash
ID="<paste-id-from-create>"

curl -X PUT http://localhost:8080/api/orders/$ID/confirm
curl -X PUT http://localhost:8080/api/orders/$ID/ship
curl -X PUT http://localhost:8080/api/orders/$ID/complete

# Check final state
curl http://localhost:8080/api/orders/$ID
```

### Error responses

Domain and transition errors return RFC 7807 **Problem Details**:

```json
{
  "status": 400,
  "title": "DomainException",
  "detail": "Cannot apply trigger 'Ship' when order is in state 'Pending'."
}
```

---

## Design Decisions

### Clean Architecture layers

Dependencies flow inward: `API → Application → Domain`.  
`Infrastructure` implements interfaces defined in `Application` — so the
domain and use-cases have **zero framework dependencies**.

### MediatR pipeline behaviours

`LoggingBehavior<TRequest,TResponse>` is registered as an open generic
pipeline behaviour, giving **structured logging + timing** for every
Command and Query without touching individual handlers.

### Rebus + RabbitMQ

Rebus acts as the **integration event bus**. After a command mutates state,
the handler publishes a typed event record (`IEventPublisher`), which Rebus
routes to RabbitMQ. Subscribers (also in Infrastructure) react asynchronously —
decoupling side-effects from the command path.

The `IEventPublisher` abstraction in the Application layer means handlers
never reference Rebus directly, keeping the transport swappable.

### Stateless state machine inside the aggregate

The state machine lives **inside the `Order` aggregate** as a private field.
It is rebuilt on every instantiation (including EF Core hydration), reads the
persisted `State` column, and mutates it only through domain methods
(`Confirm()`, `Ship()`, etc.).  
This keeps the machine as an enforcement mechanism — not just documentation.

### EF Core InMemory

Used for simplicity in this PoC. Swap `UseInMemoryDatabase` for
`UseSqlServer` / `UseNpgsql` / etc. in `Infrastructure/DependencyInjection.cs`
and add a migration to go production-ready.
