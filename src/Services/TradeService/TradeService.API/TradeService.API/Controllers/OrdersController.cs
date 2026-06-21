using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using TradingService.API.Extensions;
using TradingService.API.Models;
using TradingService.Application.Common.Models;
using TradingService.Application.DTOs;
using TradingService.Application.Features.Orders.Commands.CancelOrder;
using TradingService.Application.Features.Orders.Commands.SubmitOrder;
using TradingService.Application.Features.Orders.Queries.GetOrderById;
using TradingService.Application.Features.Orders.Queries.GetOrders;
using TradingService.Domain.Enums;

namespace TradingService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _mediator;

    public OrdersController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Submits a new order.</summary>
    /// <response code="201">Order submitted (or an existing order returned for a duplicate idempotency key).</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">The order was rejected by the pre-trade risk check.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SubmitOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitOrderResult>> SubmitOrder(
        [FromBody] SubmitOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new SubmitOrderCommand(
            User.GetUserId(),
            request.Symbol,
            request.Side,
            request.Type,
            request.Price,
            request.Quantity,
            request.IdempotencyKey);

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(this, value => CreatedAtAction(
            nameof(GetOrderById), new { id = value.OrderId }, value));
    }

    /// <summary>Cancels an open or partially-filled order.</summary>
    /// <response code="204">The order was cancelled.</response>
    /// <response code="404">No order with the given id exists.</response>
    /// <response code="409">The order is not in a cancellable state.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(id, User.GetUserId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(this, StatusCodes.Status204NoContent);
    }

    /// <summary>Lists the authenticated user's orders, optionally filtered by symbol/status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetOrders(
        [FromQuery] string? symbol,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(User.GetUserId(), symbol, status, page, pageSize);

        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Fetches a single order by id.</summary>
    /// <response code="404">No order with the given id exists, or it does not belong to the caller.</response>
    [HttpGet("{id:guid}", Name = nameof(GetOrderById))]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id, User.GetUserId());

        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(this);
    }
}