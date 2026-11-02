using System.Text.Json;
using RedisPractice.Api.Models;
using StackExchange.Redis;

namespace RedisPractice.Api.Services;

public class ProductCacheService
{
    private readonly IDatabase _redis;

    public ProductCacheService(IConnectionMultiplexer connection)
    {
        _redis = connection.GetDatabase();
    }
    public async Task<Product?> GetAsync(Guid id)
    {
        string key = $"product:{id}";

        RedisValue cachedData = await _redis.StringGetAsync(key);

        if (cachedData.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Product>(cachedData.ToString());
    }

    public async Task SetAsync(Product product)
    {
        string key = $"product:{product.Id}";

        string json = JsonSerializer.Serialize(product);

        await _redis.StringSetAsync(key, json, TimeSpan.FromMinutes(10));
    }

    public async Task RemoveAsync(Guid id)
    {
        string key = $"product:{id}";

        await _redis.KeyDeleteAsync(key);
    }
}   