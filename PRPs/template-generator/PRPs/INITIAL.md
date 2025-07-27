# Template Generation Request

## TECHNOLOGY/FRAMEWORK:

.NET 9 C# Worker Service using Clean Architecture, with MassTransit for message brokering, PostgreSQL for data persistence, and OpenTelemetry for observability.

---

## TEMPLATE PURPOSE:

To provide a production-ready starting point for building resilient, observable, and maintainable background processing services in .NET. The template will follow Clean Architecture principles to ensure separation of concerns and testability.

---

## CORE FEATURES:

- **Project Structure:** A solution (`.sln`) pre-configured with a Clean Architecture layout (Domain, Application, Infrastructure, Worker).
- **.NET 9 Worker Service:** A `Program.cs` configured for a long-running service with graceful shutdown.
- **MassTransit Integration:**
    - Connection setup for a message broker (e.g., RabbitMQ).
    - Example message consumer implementation.
    - Dependency injection for MassTransit services.
- **PostgreSQL Persistence:**
    - Entity Framework Core setup for PostgreSQL.
    - Repository pattern implementation for data access.
    - Database context configuration and migrations.
- **Observability:**
    - Pre-configured OpenTelemetry SDK for tracing, metrics, and logging.
    - Integration with MassTransit and EF Core for automatic instrumentation.
    - Console and OTLP (OpenTelemetry Protocol) exporters configured.
- **Health Checks:** ASP.NET Core Health Checks integrated into a minimal API endpoint (e.g., `/health`) to report the status of the database and message broker.

---

## EXAMPLES TO INCLUDE:

- **End-to-End Workflow:** A complete example showing a `CreateOrder` message being published, consumed by the worker, processed by an application service, and saved to the PostgreSQL database.
- **Health Check Endpoint:** A working `/health` endpoint that can be queried to see the service status.
- **Unit & Integration Tests:**
    - Unit tests for an application service (business logic).
    - An integration test using the MassTransit test harness to verify the message consumer logic against an in-memory database.

---

## DOCUMENTATION TO RESEARCH:

- https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host - .NET Generic Host / Worker Services
- https://masstransit.io/ - MassTransit Official Documentation
- https://www.npgsql.org/efcore/ - Entity Framework Core Provider for PostgreSQL
- https://opentelemetry.io/docs/instrumentation/net/ - OpenTelemetry .NET SDK
- https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks - ASP.NET Core Health Checks
- Clean Architecture by Robert C. Martin (and common .NET implementations like Jason Taylor's).

---

## DEVELOPMENT PATTERNS:

- **Clean Architecture:** Strict dependency rule enforcement (Domain -> Application -> Infrastructure/Worker).
- **Repository Pattern:** Abstracting data access logic in the Infrastructure layer.
- **CQRS (Command Query Responsibility Segregation):** Using distinct message types for commands (e.g., `CreateOrder`) and potentially queries.
- **Dependency Injection:** Extensive use of DI in `Program.cs` to wire up all services (MassTransit, EF Core, application services).
- **Configuration Management:** Using `appsettings.json` and environment variables for connection strings and other settings.
- **EF Core Migrations:** Workflow for managing database schema changes.

---

## SECURITY & BEST PRACTICES:

- **Connection String Management:** Use of .NET User Secrets for local development and environment variables for production.
- **Idempotent Consumers:** Designing MassTransit consumers to safely handle duplicate messages.
- **Graceful Shutdown:** Ensuring the worker stops listening for new messages and finishes processing in-flight messages before exiting.
- **Structured Logging:** Using structured logs (e.g., with Serilog and OpenTelemetry) to enable better querying and analysis.
- **Least Privilege Database Access:** The connection user for the service should only have the permissions it needs.

---

## COMMON GOTCHAS:

- **EF Core `DbContext` Lifecycle:** Managing the `DbContext` scope correctly within a MassTransit consumer.
- **MassTransit Endpoint Configuration:** Correctly configuring exchanges, queues, and routing for the chosen transport.
- **OpenTelemetry Exporter Configuration:** Setting up the correct endpoint and headers for the OTLP exporter.
- **Clean Architecture Dependency Leaks:** Accidentally referencing Infrastructure projects from the Domain or Application layers.
- **Asynchronous Operations:** Ensuring all async/await calls are handled correctly throughout the stack to prevent deadlocks.

---

## VALIDATION REQUIREMENTS:

- **Integration Testing:** The template must include integration tests that spin up a test harness for MassTransit and an in-memory or containerized database to validate the full message flow.
- **Static Analysis:** Include an `.editorconfig` and potentially an analyzer package (like Roslynator) to enforce coding standards.
- **Health Check Validation:** Tests to ensure the `/health` endpoint accurately reflects the state of its dependencies.
- **Trace Validation:** A test or example showing how to verify that a trace is correctly propagated from the message publisher to the consumer and through the database call.

---

## INTEGRATION FOCUS:

- **Message Broker:** RabbitMQ as the default, with notes on how to switch to Azure Service Bus or Amazon SQS.
- **Observability Backend:** Instructions on how to connect to a local observability stack using Docker Compose (e.g., Jaeger/Prometheus/Grafana).
- **Containerization:** A `Dockerfile` for building the worker service into a container image.

---

## ADDITIONAL NOTES:

- The template should include a `docker-compose.yml` file to easily spin up a local RabbitMQ and PostgreSQL instance for development.
- Emphasize the use of C# 12 and .NET 9 features where appropriate.

---

## TEMPLATE COMPLEXITY LEVEL:

- [ ] **Beginner-friendly** - Simple getting started patterns
- [X] **Intermediate** - Production-ready patterns with common features  
- [ ] **Advanced** - Comprehensive patterns including complex scenarios
- [ ] **Enterprise** - Full enterprise patterns with monitoring, scaling, security

**Reasoning:** This template targets developers building real-world, production-grade services. It includes multiple integrated technologies and follows a formal architectural pattern, which is beyond a "beginner" scope. However, it focuses on the core, essential features needed for a typical service, making "Intermediate" the perfect fit.

---

**REMINDER: Be as specific as possible in each section. The more detailed you are here, the better the generated template will be. This INITIAL.md file is where you should put all your requirements, not just basic information.**