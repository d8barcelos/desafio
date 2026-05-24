using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Extensions;
using OrderService.Application.Orders;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Entities;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orders.CreateAsync(request, cancellationToken);
        return result.ToActionResult(this, value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orders.ConfirmAsync(id, cancellationToken);
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orders.CancelAsync(id, cancellationToken);
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orders.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListOrdersQueryParameters parameters, CancellationToken cancellationToken)
    {
        var query = new ListOrdersQuery(
            parameters.CustomerId,
            parameters.Status,
            parameters.From,
            parameters.To,
            parameters.Page ?? 1,
            parameters.PageSize ?? 20);

        var result = await _orders.ListAsync(query, cancellationToken);
        return result.ToActionResult(this);
    }
}

public sealed class ListOrdersQueryParameters
{
    public Guid? CustomerId { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
