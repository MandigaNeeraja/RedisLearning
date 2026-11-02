using Microsoft.AspNetCore.Mvc;
using RedisPractice.Api.Models;
using RedisPractice.Api.Services;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _products;

    public ProductsController(ProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAll()
    {
        return Ok(await _products.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetById(Guid id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(Product request)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        var product = await _products.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Product>> Update(Guid id, Product request)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        var product = await _products.UpdateAsync(id, request);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await _products.DeleteAsync(id))
        {
            return NotFound();
        }

        return NoContent();
    }

    private static string? Validate(Product request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Price < 0)
        {
            return "Price cannot be negative.";
        }

        return null;
    }
}
