using Microsoft.EntityFrameworkCore;
using RedisPractice.Api.Data;
using RedisPractice.Api.Models;
using RedisPractice.Api.Services;

public sealed class ProductService
{
    private readonly RedisPracticeDbContext _db;
    private readonly ProductCacheService _cache;
    public ProductService(RedisPracticeDbContext db, ProductCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        return await _db.Products.AsNoTracking().ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        // 1. check redis first
        Product? cachedProduct = await _cache.GetAsync(id);
        if (cachedProduct is not null)
        {
            return cachedProduct;
        }

        //2. cache miss: fetch from sql
        Product? product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id);
        if (product is not null)
        {
            await _cache.SetAsync(product);
            return product;
        }

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
