using Microsoft.EntityFrameworkCore;
using RedisPractice.Api.Data;
using RedisPractice.Api.Models;
using RedisPractice.Api.Services;
using System.Diagnostics;
using Microsoft.Extensions.Caching;

public sealed class ProductService
{
    private readonly RedisPracticeDbContext _db;
    private readonly ProductCacheService _cache;
    private readonly ILogger<ProductService> _logger;
    public ProductService(RedisPracticeDbContext db, ProductCacheService cache, ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        return await _db.Products.AsNoTracking().ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        //total time : This is the total time taken to get the product from the database
        var totalTime = Stopwatch.StartNew();

        // 1. check redis first
        // redisTime : This is the time taken to check the redis cache
        var redisTime = Stopwatch.StartNew();
        Product? cachedProduct = await _cache.GetAsync(id);
        redisTime.Stop();
        if (cachedProduct is not null)
        {
            //stopping total time because we found the product in the redis cache
            totalTime.Stop();
            _logger.LogInformation("CACHE HIT | PRODUCT ID: {ProductId} | FETCHED FROM REDIS IN {RedisTime}ms | TOTAL TIME: {TotalTime}ms", id, redisTime.ElapsedMilliseconds, totalTime.ElapsedMilliseconds);


    
            return cachedProduct;
        }

        //2. cache miss: fetch from sql
        _logger.LogInformation("CACHE MISS | PRODUCT ID: {ProductId} | FETCHING FROM SQL", id);

        var databaseTime = Stopwatch.StartNew();
        Product? product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id);
        databaseTime.Stop();
        if (product is not null)
        {
            await _cache.SetAsync(product);
            totalTime.Stop();
            _logger.LogInformation("CACHE SET | PRODUCT ID: {ProductId} | SAVED TO REDIS IN {RedisTime}ms | TOTAL TIME: {TotalTime}ms", id, databaseTime.ElapsedMilliseconds, totalTime.ElapsedMilliseconds);
            return product;
        }
        
        totalTime.Stop();
        _logger.LogInformation("Product not found | PRODUCT ID: {ProductId} | TOTAL TIME: {TotalTime}ms", id, totalTime.ElapsedMilliseconds);
        return null;
    }

    public async Task<Product> CreateAsync(Product request)
    {
        var product = new Product
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            Category = request.Category.Trim()
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await _cache.SetAsync(product);
        return product;
    }

    public async Task<Product?> UpdateAsync(Guid id, Product request)
    {
        var product = await _db.Products.FirstOrDefaultAsync(existing => existing.Id == id);
        if (product is null)
        {
            return null;
        }

        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.Category = request.Category.Trim();
        await _db.SaveChangesAsync();
        await _cache.SetAsync(product);
        return product;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(existing => existing.Id == id);
        if (product is null)
        {
            return false;
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync(id);
        return true;
    }
}
