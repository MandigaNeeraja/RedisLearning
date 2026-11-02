using Microsoft.EntityFrameworkCore;
using RedisPractice.Api.Models;

namespace RedisPractice.Api.Data;

public class RedisPracticeDbContext : DbContext
{
    public RedisPracticeDbContext(DbContextOptions<RedisPracticeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
}