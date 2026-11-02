public sealed class ItemStore
{
    private readonly List<Item> _items = [];
    private readonly Lock _lock = new();
    private int _nextId = 1;

    public IReadOnlyList<Item> GetAll()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    public Item? GetById(int id)
    {
        lock (_lock)
        {
            return _items.FirstOrDefault(item => item.Id == id);
        }
    }

    public Item Add(string name)
    {
        lock (_lock)
        {
            var item = new Item
            {
                Id = _nextId++,
                Name = name.Trim()
            };
            _items.Add(item);
            return item;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(existing => existing.Id == id);
            if (item is null)
            {
                return false;
            }

            _items.Remove(item);
            return true;
        }
    }
}
