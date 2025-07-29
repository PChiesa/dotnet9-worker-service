using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Queries;

namespace WorkerService.Worker.Endpoints;

public static class ItemEndpoints
{
    public static RouteGroupBuilder MapItemEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/items
        group.MapPost("/", CreateItem)
            .WithName("CreateItem")
            .WithSummary("Create a new item")
            .WithDescription("Creates a new item in the product catalog")
            .Produces<ItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /api/items/{id}
        group.MapGet("/{id:guid}", GetItem)
            .WithName("GetItem")
            .WithSummary("Get item by ID")
            .WithDescription("Retrieves a specific item by its ID")
            .Produces<ItemDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/items
        group.MapGet("/", GetItems)
            .WithName("GetItems")
            .WithSummary("Get paginated list of items")
            .WithDescription("Retrieves a paginated list of items with optional filtering")
            .Produces<PagedItemsResult>();

        // GET /api/items/sku/{sku}
        group.MapGet("/sku/{sku}", GetItemBySku)
            .WithName("GetItemBySku")
            .WithSummary("Get item by SKU")
            .WithDescription("Retrieves a specific item by its SKU")
            .Produces<ItemDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PUT /api/items/{id}
        group.MapPut("/{id:guid}", UpdateItem)
            .WithName("UpdateItem")
            .WithSummary("Update an item")
            .WithDescription("Updates an existing item's details")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /api/items/{id}
        group.MapDelete("/{id:guid}", DeactivateItem)
            .WithName("DeactivateItem")
            .WithSummary("Deactivate an item")
            .WithDescription("Soft deletes an item by marking it as inactive")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PUT /api/items/{id}/stock
        group.MapPut("/{id:guid}/stock", AdjustStock)
            .WithName("AdjustStock")
            .WithSummary("Adjust item stock")
            .WithDescription("Updates the available stock quantity for an item")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/items/{id}/reserve-stock
        group.MapPost("/{id:guid}/reserve-stock", ReserveStock)
            .WithName("ReserveStock")
            .WithSummary("Reserve item stock")
            .WithDescription("Reserves stock for a pending order")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Created<ItemDto>, ValidationProblem, Conflict<ProblemDetails>>> CreateItem(
        CreateItemDto dto,
        IValidator<CreateItemCommand> validator,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateItemCommand(
            dto.SKU,
            dto.Name,
            dto.Description,
            dto.Price,
            dto.InitialStock,
            dto.Category
        );

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var location = $"{httpContext.Request.Path}/{result.Id}";
            return TypedResults.Created(location, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> GetItem(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        return result != null 
            ? TypedResults.Ok(result) 
            : TypedResults.NotFound();
    }

    private static async Task<Ok<PagedItemsResult>> GetItems(
        [AsParameters] ItemQueryParameters parameters,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemsQuery(
            parameters.PageNumber ?? 1,
            parameters.PageSize ?? 20,
            parameters.Category,
            parameters.IsActive,
            parameters.Search
        );

        var result = await mediator.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> GetItemBySku(
        string sku,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemBySkuQuery(sku);
        var result = await mediator.Send(query, cancellationToken);

        return result != null 
            ? TypedResults.Ok(result) 
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> UpdateItem(
        Guid id,
        UpdateItemDto dto,
        IValidator<UpdateItemCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateItemCommand(
            id,
            dto.Name,
            dto.Description,
            dto.Price,
            dto.Category
        );

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("modified by another user"))
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeactivateItem(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateItemCommand(id);
        var result = await mediator.Send(command, cancellationToken);

        return result 
            ? TypedResults.NoContent() 
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> AdjustStock(
        Guid id,
        StockAdjustmentDto dto,
        IValidator<AdjustStockCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AdjustStockCommand(id, dto.NewQuantity, dto.Reason);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> ReserveStock(
        Guid id,
        StockReservationDto dto,
        IValidator<ReserveStockCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ReserveStockCommand(id, dto.Quantity, dto.OrderId);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }
}

// Query parameters record
public record ItemQueryParameters(
    int? PageNumber,
    int? PageSize,
    string? Category,
    bool? IsActive,
    string? Search
);