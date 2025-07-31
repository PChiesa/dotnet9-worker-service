# Feature: Full Order Lifecycle Management

**Status:** Proposed
**Version:** 1.0

## 1. Summary

This feature implements the complete, end-to-end order processing workflow as envisioned in the project's `README.md`. It introduces the necessary commands, handlers, and API endpoints to transition an order through all its lifecycle states: from `Validated` to `Paid`, `Shipped`, `Delivered`, and `Cancelled`.

Furthermore, it leverages the existing MassTransit message bus to handle post-transition logic asynchronously, ensuring a decoupled and resilient architecture.

## 2. Goal

The primary goal is to make the order processing system fully functional by enabling administrators or automated systems to manage the entire fulfillment process. A key technical goal is to decouple business logic by using an event-driven approach for order status changes, improving system resilience and scalability.

## 3. User Stories

-   **As a System Administrator,** I want to process the payment for a validated order so that it can be marked as `Paid` and prepared for shipping.
-   **As a System Administrator,** I want to mark a paid order as `Shipped` and include a tracking number, so that the fulfillment process can proceed.
-   **As a System Administrator,** I want to mark a shipped order as `Delivered` to signify the completion of the order lifecycle.
-   **As a System Administrator,** I want to be able to `Cancel` an order at any point before it is delivered to stop the fulfillment process.

## 4. Technical Requirements

This feature will be implemented by extending the existing Clean Architecture and CQRS patterns.

### Domain Layer (`WorkerService.Domain`)

-   Verify the `Order` entity contains methods for all required state transitions:
    -   `MarkAsPaid()`
    -   `MarkAsShipped(string trackingNumber)`
    -   `MarkAsDelivered()`
    -   `Cancel(string reason)`
-   Add a `TrackingNumber` property to the `Order` entity.
-   Ensure corresponding domain events are raised and published for each state change:
    -   `OrderPaidEvent`
    -   `OrderShippedEvent`
    -   `OrderDeliveredEvent`
    -   `OrderCancelledEvent`

### Application Layer (`WorkerService.Application`)

-   **Commands:** Create the following new command records:
    -   `ProcessPaymentCommand(Guid OrderId)`
    -   `ShipOrderCommand(Guid OrderId, string TrackingNumber)`
    -   `MarkOrderDeliveredCommand(Guid OrderId)`
    -   `CancelOrderCommand(Guid OrderId, string? Reason)`
-   **Handlers:** Implement a handler for each new command. Each handler will:
    1.  Retrieve the `Order` from the repository.
    2.  Call the appropriate method on the `Order` entity.
    3.  Persist the changes via the repository.
    4.  The domain events raised by the entity will be published by MediatR automatically.
-   **Validators:** Add validators for the new commands to ensure data integrity (e.g., `TrackingNumber` is not empty).

### Infrastructure Layer (`WorkerService.Infrastructure`)

-   **Database:**
    -   Update the `Order` entity configuration in `ApplicationDbContext` to include the new `TrackingNumber` property.
    -   Generate a new EF Core migration to apply the schema change.
-   **Asynchronous Consumers (MassTransit):**
    -   Create new consumer classes in the `Consumers` directory to handle the domain events asynchronously.
    -   `OrderPaidConsumer.cs`: Subscribes to `OrderPaidEvent`. Initial logic will log the event.
    -   `OrderShippedConsumer.cs`: Subscribes to `OrderShippedEvent`. Initial logic will log the event and the tracking number.
    -   `OrderCancelledConsumer.cs`: Subscribes to `OrderCancelledEvent`. Initial logic will log the event and the reason for cancellation.
    -   These consumers will serve as the foundation for future features like sending emails or notifying external systems.

### Worker Layer (`WorkerService.Worker`)

-   **API Endpoints:** Expose new minimal API endpoints in `OrdersController.cs` or a new `OrderEndpoints.cs` file to trigger the new commands:
    -   `POST /api/orders/{id}/pay` → Triggers `ProcessPaymentCommand`
    -   `POST /api/orders/{id}/ship` → Triggers `ShipOrderCommand`
    -   `POST /api/orders/{id}/deliver` → Triggers `MarkOrderDeliveredCommand`
    -   `POST /api/orders/{id}/cancel` → Triggers `CancelOrderCommand`
-   **Dependency Injection:**
    -   Register the new MassTransit consumers in `Program.cs` to connect them to the message bus.

## 5. Acceptance Criteria

-   When a `POST` request is made to `/api/orders/{id}/pay` for a `Validated` order, the order's status in the database updates to `Paid`.
-   The `OrderPaidConsumer` successfully consumes the resulting `OrderPaidEvent` from the message queue.
-   When a `POST` request with a `trackingNumber` is made to `/api/orders/{id}/ship` for a `Paid` order, the order's status updates to `Shipped` and the tracking number is saved.
-   The `OrderShippedConsumer` successfully consumes the `OrderShippedEvent`.
-   When a `POST` request is made to `/api/orders/{id}/deliver` for a `Shipped` order, its status updates to `Delivered`.
-   When a `POST` request is made to `/api/orders/{id}/cancel` for an order that is not `Delivered`, its status updates to `Cancelled`.
-   The `OrderCancelledConsumer` successfully consumes the `OrderCancelledEvent`.
-   Attempting to trigger an invalid state transition results in a validation error or bad request response.

## 6. Out of Scope

-   Complex business logic within the new consumers. Initial implementation will be limited to logging.
-   Integration with third-party payment gateways or shipping carriers.
-   User-facing notifications (e.g., emails, SMS).
