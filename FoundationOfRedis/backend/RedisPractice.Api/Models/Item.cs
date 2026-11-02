public sealed class Item
{
    public int Id { get; set; }

    public required string Name { get; set; }
}

public sealed class CreateItemRequest
{
    public required string Name { get; set; }
}
