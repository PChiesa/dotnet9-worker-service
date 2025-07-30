# Gemini Code-Assist Configuration (GEMINI.md)

This document provides guidance for AI-powered developer tools to effectively understand and contribute to the `dotnet9-worker-service` project.

## 1. Project Overview

This project is a .NET 9 Worker Service designed to handle background processing tasks, likely related to order and item management. It is built following the principles of **Clean Architecture** to ensure a clear separation of concerns, maintainability, and testability. The service also exposes a minimal set of API endpoints for direct interaction.

The core of the application uses the **Command Query Responsibility Segregation (CQRS)** pattern, orchestrated by the MediatR library, to separate read and write operations.

## 2. Key Technologies & Libraries

- **.NET 9:** The underlying framework for the application.
- **ASP.NET Core:** Used for hosting the worker service and exposing minimal API endpoints.
- **Entity Framework Core:** The primary Object-Relational Mapper (ORM) for data access.
- **MediatR:** Implements the CQRS pattern, decoupling the dispatch of requests (Commands and Queries) from their handlers.
- **FluentValidation:** Provides a fluent interface for building strongly-typed validation rules.
- **MassTransit:** A framework for building message-based applications, used here for asynchronous communication (e.g., consuming events).
- **xUnit:** The primary framework for writing unit and integration tests.
- **Moq:** A mocking library used to isolate dependencies in unit tests.
- **FluentAssertions:** Provides a more readable and expressive way to write assertions in tests.
- **Docker:** The project includes `docker-compose.yml` and `prometheus.yml`, indicating containerization support and monitoring with Prometheus.

## 3. Architectural Patterns

### Clean Architecture

The solution is divided into four main projects, representing the layers of Clean Architecture:

- `WorkerService.Domain`: Contains the core business logic and entities. It has no dependencies on other layers.
  - **Entities:** Plain C# objects representing the core concepts (e.g., `Order`, `Item`).
  - **ValueObjects:** Immutable objects representing concepts like `Price` and `SKU`.
  - **Interfaces:** Defines contracts for repositories (`IOrderRepository`, `IItemRepository`).
  - **Events:** Domain events that are raised when significant state changes occur.

- `WorkerService.Application`: Contains the application logic. It orchestrates the domain layer to perform business operations.
  - **CQRS (Commands & Queries):** Defines the requests that the application can handle.
    - **Commands:** Represent an intent to change the state of the system (e.g., `CreateOrderCommand`).
    - **Queries:** Represent a request for data (e.g., `GetOrderQuery`).
  - **Handlers:** Implement the logic to handle specific commands and queries. They contain the main application logic.
  - **Validators:** Use FluentValidation to define validation rules for incoming commands.

- `WorkerService.Infrastructure`: Contains the implementation details for external concerns like databases, message brokers, and other services.
  - **Data:** Contains the Entity Framework Core `DbContext` and repository implementations.
  - **Consumers:** Contains MassTransit message consumers.
  - **Migrations:** EF Core database migration files.

- `WorkerService.Worker`: The composition root and host for the application.
  - **Program.cs:** Configures and starts the worker service, registers dependencies, and sets up middleware.
  - **Endpoints:** Defines the minimal API endpoints.
  - **Services:** Contains background services that run as part of the worker.

### CQRS (Command Query Responsibility Segregation)

- **Commands:** To add a new write operation, create a new command record in `src/WorkerService.Application/Commands`. Then, create a corresponding handler in `src/WorkerService.Application/Handlers`. If validation is needed, add a validator in `src/WorkerService.Application/Validators`.
- **Queries:** To add a new read operation, create a new query record in `src/WorkerService.Application/Queries` and a corresponding handler in `src/WorkerService.Application/Handlers`.

## 4. Development Workflow

### Adding a New Feature (e.g., "Cancel Order")

1.  **Domain:** If the concept of a "cancelled" order doesn't exist, first update the `OrderStatus` enum and the `Order` entity in `WorkerService.Domain`.
2.  **Application (Command):**
    - Create a `CancelOrderCommand.cs` file in `src/WorkerService.Application/Commands`.
    - Create a `CancelOrderCommandHandler.cs` in `src/WorkerService.Application/Handlers`. This handler will fetch the order, perform the cancellation logic, and save the changes using the `IOrderRepository`.
    - (Optional) Create a `CancelOrderCommandValidator.cs` in `src/WorkerService.Application/Validators`.
3.  **Infrastructure:** No changes are likely needed here unless the operation requires a new external service.
4.  **Worker (Endpoint):** If the cancellation should be triggered via an API call, add a new endpoint in `src/WorkerService.Worker/Endpoints/` (or `Controllers`).
5.  **Testing:**
    - Add a unit test in `tests/WorkerService.UnitTests/Handlers` for the `CancelOrderCommandHandlerTests.cs`. Mock dependencies like the repository.
    - Add an integration test in `tests/WorkerService.IntegrationTests/` to verify the feature end-to-end, from the API endpoint to the database.

## 5. Testing Strategy

- **Unit Tests (`tests/WorkerService.UnitTests`):**
  - Focus on testing individual components in isolation (e.g., a single command handler, a domain entity's logic).
  - Use **Moq** to mock dependencies (like repositories or external services).
  - Use the **EF Core In-Memory Provider** for simple data access tests where appropriate.
  - Run with the `dotnet test` command.

- **Integration Tests (`tests/WorkerService.IntegrationTests`):**
  - Test the interaction between multiple components.
  - These tests often use a real database (spun up in a test container) or a more complete in-memory setup to test the full application stack.
  - The `Container` and `InMemory` folders suggest different test setups. Adhere to the existing structure when adding new tests.
  - Run with the `dotnet test` command.

By following these guidelines, you can help maintain the architectural integrity and quality of the codebase.
