﻿using System.Linq.Dynamic.Core;
using System.Net;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPM_Project_Backend.Domain;
using RPM_Project_Backend.Helpers;
using RPM_Project_Backend.Models;
using RPM_Project_Backend.Services.Database;

namespace RPM_Project_Backend.Controllers;

/// <inheritdoc />
[ApiController]
[ApiVersion("1.0")]
[Route("api/orders")]
[EnableCors("MyPolicy")]
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;
    private readonly ApplicationContext _context;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public OrdersController(ILogger<OrdersController> logger, ApplicationContext context, IMapper mapper)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
    }
    
    /// <summary>
    /// Get orders list
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// GET api/orders
    /// 
    /// </remarks>
    /// <response code="200">Return orders list</response>
    /// <response code="404">Orders not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<OrderResponseDto>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> Get(
        [FromQuery] QueryParameters<object> queryParameters)
    {
        _logger.LogDebug("Get list of orders");

        var userId = int.Parse(User.Claims.First(cl => cl.Type == "id").Value);

        var orders = _context.Orders
            .Where(o => o.UserId == userId && o.UserId == userId)
            .OrderBy(queryParameters.OrderBy, queryParameters.IsDescending());

        var paginationMetadata = new
        {
            totalCount = orders.Count(),
            pageSize = orders.Count() < queryParameters.PageCount
                ? orders.Count()
                : queryParameters.PageCount,
            currentPage = queryParameters.GetTotalPages(orders.Count()) < queryParameters.Page
                ? (int)queryParameters.GetTotalPages(orders.Count())
                : queryParameters.Page,
            totalPages = queryParameters.GetTotalPages(orders.Count())
        };

        Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(paginationMetadata));
        
        if (queryParameters is {Page: <= 0} or {PageCount: <= 0})
            return NotFound(new ErrorModel("Orders not found"));

        return await orders.CountAsync() switch
        {
            0 => NotFound(new ErrorModel("Orders not found")),
            _ => Ok(
                orders
                    .Skip(paginationMetadata.pageSize * (paginationMetadata.currentPage - 1))
                    .Take(paginationMetadata.pageSize)
                    .Select(o => _mapper.Map<OrderResponseDto>(o))
            )
        };
    }
    
    /// <summary>
    /// Get single order
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// GET api/orders/1
    /// 
    /// </remarks>
    /// <response code="200">Return single order</response>
    /// <response code="404">Order not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(OrderResponseDto), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<OrderResponseDto>> Get(
        int id)
    {
        if (id <= 0)
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Get order with id = {id}", id);

        var userId = int.Parse(User.Claims.First(cl => cl.Type == "id").Value);

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == id);

        return order switch
        {
            null => NotFound(new ErrorModel("Order not found")),
            _ => Ok(_mapper.Map<OrderResponseDto>(order))
        };
    }
    
    /// <summary>
    /// Purchase single order
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// POST api/orders/1
    /// 
    /// </remarks>
    /// <response code="200">Return single order</response>
    /// <response code="404">Order not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [HttpPost("buy/{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(OrderResponseDto), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<OrderResponseDto>> Post(
        int id,
        [FromBody]int paymentId)
    {
        if (id <= 0 || paymentId <= 0)
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Get order with id = {id}", id);

        var userId = int.Parse(User.Claims.First(cl => cl.Type == "id").Value);
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId);
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == id);
        
        if (payment is null) 
            return NotFound(new ErrorModel("Payment method not found"));
        if (order is null) 
            return NotFound(new ErrorModel("Order not found"));

        var transaction = new Transaction
        {
            OrderId = order.Id,
            PaymentId = payment.Id
        };

        await _context.Transactions.AddAsync(transaction);
        order.Status = "Paid";
        _context.Entry(order).State = EntityState.Modified;

        return await _context.SaveChangesAsync() switch
        {
            0 => StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred")),
            _ => Ok(_mapper.Map<OrderResponseDto>(order))
        };
    }
    
    /// <summary>
    /// Delete order
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// DELETE api/orders/1
    /// 
    /// </remarks>
    /// <param name="id"></param>
    /// <response code="204">Delete order</response>
    /// <response code="400">The input data is empty</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="404">Order not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [Authorize]
    [HttpDelete]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorModel), (int) HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Delete(
        int id)
    {
        if (id <= 0)
            return BadRequest(new ErrorModel("The input data is empty"));
        
        var order = await _context.Orders
            .FirstOrDefaultAsync(p => p.Id == id);
        
        if (order is null) 
            return NotFound(new ErrorModel("Order not found"));

        var userId = int.Parse(User.Claims.First(cl => cl.Type == "id").Value);
        
        if (userId != order.UserId)
            return Unauthorized(new ErrorModel("Access is denied"));

        _logger.LogDebug("Remove order with id = {id}", id);
        
        var ordersHaveProducts = _context.OrdersHaveProducts
            .Where(ohp => ohp.OrderId == order.Id);

        var usersHaveProducts = ordersHaveProducts
            .Select(product => 
                new UsersHaveProducts()
                {
                    UserId = userId, 
                    ProductId = product.ProductId
                }).ToList();

        _context.OrdersHaveProducts.RemoveRange(ordersHaveProducts);
        await _context.UsersHaveProducts.AddRangeAsync(usersHaveProducts);

        return await _context.SaveChangesAsync() switch
        {
            0 => StatusCode(StatusCodes.Status500InternalServerError, new ErrorModel("Some error has occurred")),
            _ => NoContent()
        };
    }
}