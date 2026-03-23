using CqrsPoC.Application;
using CqrsPoC.Domain.Exceptions;
using CqrsPoC.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "CQRS PoC — Order Management API",
            Version = "v1",
            Description = """
            Proof of Concept demonstrating CQRS in .NET 10.

            **Stack:** MediatR · Rebus · RabbitMQ · Stateless State Machine · EF Core

            **Order lifecycle:**
            ```
            Pending ──[confirm]──► Confirmed ──[ship]──► Shipped ──[complete]──► Completed
              │                        │
              └──────────[cancel]──────┘──────────────────────────────────────► Cancelled
            ```
            """,
        }
    );
    c.EnableAnnotations();
});

// ── App ───────────────────────────────────────────────────────────────────────

var app = builder.Build();

// Global exception handler that maps domain exceptions to HTTP responses
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature =
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is null)
            return;

        var (status, message) = feature.Error switch
        {
            OrderNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            DomainException ex => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = feature.Error.GetType().Name,
                Detail = message,
            }
        );
    });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CQRS PoC v1");
    c.RoutePrefix = string.Empty; // Swagger at root
});

app.UseRouting();
app.MapControllers();

// Subscribe Rebus to all domain events after the app is built
await app.Services.SubscribeToEventsAsync();

app.Run();

// Make the implicit Program class visible to the E2E test project
public partial class Program { }
