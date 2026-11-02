using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

[ApiController]
[Route("api/[controller]")]
public sealed class RedisController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;

    public RedisController(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }
    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        var result = await _redis.GetDatabase().ExecuteAsync("PING");
        return Ok(new { redis = result.ToString() });
    }
    [HttpGet("keys")]
    public IActionResult GetKeys()
    {
        var endpoint = _redis.GetEndPoints().First();
        var server = _redis.GetServer(endpoint);
        var db = _redis.GetDatabase();

        var entries = new List<object>();
        foreach (var key in server.Keys(database: db.Database, pattern: "*"))
        {
            var value = db.StringGet(key);
            entries.Add(new { key = key.ToString(), value = value.ToString() });
        }

        return Ok(new { count = entries.Count, entries });
    }
}