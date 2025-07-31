# PRP: Full Order Lifecycle Management

## Feature Title
Full Order Lifecycle Management - Complete order processing workflow with state transitions and event-driven architecture

## Technical Overview
This feature implements the complete end-to-end order processing workflow by extending the existing Clean Architecture and CQRS patterns. The implementation adds new commands and handlers to transition orders through all lifecycle states (Validated → Paid → Shipped → Delivered, with Cancelled option), leverages MassTransit for asynchronous event processing, and introduces new API endpoints for order lifecycle management.

The solution follows the existing architectural patterns with proper separation of concerns across Domain, Application, Infrastructure, and Worker layers. Key enhancements include adding a TrackingNumber property to the Order entity, implementing new domain events, creating MassTransit consumers for event handling, and exposing RESTful API endpoints.

## Effort Score
**7/10** - Moderately complex feature requiring changes across all layers, database migration, new consumers, and comprehensive testing

## Success Chance Score
**9/10** - High confidence due to well-established patterns in the codebase and clear architectural guidelines

## Implementation Steps

### Phase 1: Domain Layer Enhancements (`WorkerService.Domain`)

#### 1.1 Update Order Entity with TrackingNumber Property
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Domain\Entities\Order.cs`
- Add `public string? TrackingNumber { get; private set; }` property
- Update `MarkAsShipped()` method to accept `string trackingNumber` parameter and set the property
- Update `Cancel(string? reason)` method to accept optional reason parameter
- Ensure proper validation in state transition methods

#### 1.2 Enhance Domain Events
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Domain\Events\OrderEvents.cs`
- Add `OrderDeliveredEvent` record with `Guid OrderId` and `string CustomerId` properties
- Update `OrderShippedEvent` to include `string TrackingNumber` property
- Update `OrderCancelledEvent` to include `string? Reason` property
- Ensure all events follow the existing IDomainEvent pattern

### Phase 2: Application Layer Commands and Handlers (`WorkerService.Application`)

#### 2.1 Create New Command Records
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Commands\ProcessPaymentCommand.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Commands\ShipOrderCommand.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Commands\MarkOrderDeliveredCommand.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Commands\CancelOrderCommand.cs`

Each command should:
- Follow the existing command pattern (record types)
- Include appropriate properties (OrderId for all, TrackingNumber for ship, Reason for cancel)
- Implement IRequest<bool> for success/failure responses

#### 2.2 Create Command Handlers
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Handlers\ProcessPaymentCommandHandler.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Handlers\ShipOrderCommandHandler.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Handlers\MarkOrderDeliveredCommandHandler.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Handlers\CancelOrderCommandHandler.cs`

Each handler should:
- Follow the existing handler pattern (IRequestHandler<TCommand, bool>)
- Retrieve order from repository
- Call appropriate domain method
- Save changes via repository
- Handle domain events automatically through MediatR

#### 2.3 Create Command Validators
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Validators\ProcessPaymentCommandValidator.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Validators\ShipOrderCommandValidator.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Validators\MarkOrderDeliveredCommandValidator.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Application\Validators\CancelOrderCommandValidator.cs`

Validators should ensure:
- OrderId is not empty
- TrackingNumber is not empty for ship command
- Follow FluentValidation patterns from existing validators

### Phase 3: Infrastructure Layer Enhancements (`WorkerService.Infrastructure`)

#### 3.1 Database Schema Update
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Infrastructure\Data\ApplicationDbContext.cs`
- Add TrackingNumber property configuration in Order entity mapping
- Set appropriate column constraints (MaxLength(100), nullable)

**Action:** Generate EF Core migration
```bash
dotnet ef migrations add AddTrackingNumberToOrder --project src/WorkerService.Infrastructure --startup-project src/WorkerService.Worker
```

#### 3.2 Create MassTransit Consumers
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Infrastructure\Consumers\OrderPaidConsumer.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Infrastructure\Consumers\OrderShippedConsumer.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Infrastructure\Consumers\OrderDeliveredConsumer.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Infrastructure\Consumers\OrderCancelledConsumer.cs`

Each consumer should:
- Implement IConsumer<TEvent> pattern
- Include structured logging with correlation IDs
- Handle events asynchronously
- Follow the pattern established in OrderCreatedConsumer
- Initial implementation focuses on logging for future extensibility

### Phase 4: Worker Layer API Endpoints (`WorkerService.Worker`)

#### 4.1 Extend OrdersController with Lifecycle Endpoints
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Controllers\OrdersController.cs`

Add new endpoints:
- `POST /api/orders/{id}/pay` → ProcessPaymentCommand
- `POST /api/orders/{id}/ship` → ShipOrderCommand (with request body containing TrackingNumber)
- `POST /api/orders/{id}/deliver` → MarkOrderDeliveredCommand  
- `POST /api/orders/{id}/cancel` → CancelOrderCommand (with optional reason in request body)

Each endpoint should:
- Follow existing controller patterns (authorization, validation, error handling)
- Include proper OpenAPI documentation
- Return appropriate HTTP status codes
- Include structured logging

#### 4.2 Update Program.cs Configuration
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Program.cs`
- Register new MassTransit consumers in both in-memory and RabbitMQ configurations
- Add receive endpoints for new events in RabbitMQ configuration
- Ensure proper quorum queue configuration for production

### Phase 5: Testing Strategy

#### 5.1 Unit Tests - Domain Layer
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Domain\OrderLifecycleTests.cs`

Test scenarios:
- Valid state transitions (Validated→Paid→Shipped→Delivered)
- Invalid state transitions (e.g., Pending→Shipped)
- TrackingNumber assignment during shipping
- Cancellation from various states
- Domain event raising for each transition

#### 5.2 Unit Tests - Application Layer
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Handlers\ProcessPaymentCommandHandlerTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Handlers\ShipOrderCommandHandlerTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Handlers\MarkOrderDeliveredCommandHandlerTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Handlers\CancelOrderCommandHandlerTests.cs`

Test scenarios:
- Successful command execution
- Order not found scenarios
- Invalid state transition handling
- Repository interaction verification

#### 5.3 Unit Tests - Validators
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Validators\ProcessPaymentCommandValidatorTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Validators\ShipOrderCommandValidatorTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Validators\MarkOrderDeliveredCommandValidatorTests.cs`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Validators\CancelOrderCommandValidatorTests.cs`

#### 5.4 Integration Tests - API Endpoints
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\InMemory\Tests\OrderLifecycleApiInMemoryTests.cs`

Test scenarios:
- End-to-end order lifecycle flow
- Invalid state transition API responses
- Authentication/authorization enforcement
- Request validation and error responses

#### 5.5 Integration Tests - Message Flow
**File:** `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\InMemory\Tests\OrderLifecycleMessageFlowInMemoryTests.cs`

Test scenarios:
- Event publishing and consumption
- Consumer message handling
- Event correlation and logging

## Database Migration Requirements

### Migration Details
**Migration Name:** `AddTrackingNumberToOrder`

**Schema Changes:**
- Add nullable `TrackingNumber` column to Orders table
- Column type: VARCHAR(100)
- Add index on TrackingNumber for performance

**Migration Script Preview:**
```sql
ALTER TABLE "Orders" ADD COLUMN "TrackingNumber" character varying(100);
CREATE INDEX "IX_Orders_TrackingNumber" ON "Orders" ("TrackingNumber");
```

## Validation and Error Handling Approach

### Command Validation
- Use FluentValidation for input validation
- Validate OrderId format and non-empty values
- Ensure TrackingNumber is provided for shipping operations
- Follow existing validation patterns in the codebase

### Domain Validation
- Leverage existing domain entity validation in state transition methods
- Provide clear error messages for invalid state transitions
- Maintain business rule integrity through domain methods

### API Error Handling
- Return appropriate HTTP status codes (400 for validation, 404 for not found, 409 for conflicts)
- Follow existing error response patterns in OrdersController
- Include structured error responses with clear messages

### Consumer Error Handling
- Implement proper exception handling in MassTransit consumers
- Use structured logging with correlation IDs
- Follow retry policies established in MassTransit configuration

## Risk Assessment and Mitigation

### Medium Risks
1. **Database Migration in Production**
   - Mitigation: Test migration thoroughly in staging environment
   - Consider adding column as nullable initially

2. **Message Queue Scaling**
   - Mitigation: Use quorum queues and proper prefetch settings
   - Monitor queue depths and consumer performance

### Low Risks
1. **API Backward Compatibility**
   - Mitigation: Only adding new endpoints, no changes to existing ones

2. **Event Processing Order**
   - Mitigation: Events are processed asynchronously, order independence by design

## Acceptance Criteria Validation

The implementation will satisfy all acceptance criteria from the FRD:

1. ✅ `POST /api/orders/{id}/pay` updates Validated orders to Paid status
2. ✅ OrderPaidConsumer processes OrderPaidEvent successfully  
3. ✅ `POST /api/orders/{id}/ship` updates Paid orders to Shipped with tracking number
4. ✅ OrderShippedConsumer processes OrderShippedEvent successfully
5. ✅ `POST /api/orders/{id}/deliver` updates Shipped orders to Delivered status
6. ✅ `POST /api/orders/{id}/cancel` updates non-Delivered orders to Cancelled status  
7. ✅ OrderCancelledConsumer processes OrderCancelledEvent successfully
8. ✅ Invalid state transitions return appropriate validation errors

## Performance Considerations

### Database Performance
- TrackingNumber column will be indexed for efficient queries
- State transition operations are atomic and optimized

### Message Processing Performance  
- Consumers are designed for high throughput with proper concurrency
- Event processing is asynchronous and non-blocking

### API Response Performance
- Commands follow CQRS pattern for optimal write performance
- Validation is performed efficiently using FluentValidation

## Future Extensibility

The implementation provides foundations for:
- Email notifications in event consumers
- Integration with external shipping APIs
- Payment gateway integration
- Order analytics and reporting
- Workflow automation triggers

This PRP ensures the Full Order Lifecycle Management feature integrates seamlessly with the existing .NET 9 Worker Service architecture while maintaining high code quality, testability, and production readiness.