using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public sealed class ItemsController : ControllerBase
{
    private readonly ItemStore _store;

    public ItemsController(ItemStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Item>> GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{id:int}")]
    public ActionResult<Item> GetById(int id)
    {
        var item = _store.GetById(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    public ActionResult<Item> Create(CreateItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        var item = _store.Add(request.Name);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        if (!_store.Delete(id))
        {
            return NotFound();
        }

        return NoContent();
    }
}
