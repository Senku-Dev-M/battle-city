using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Persistence.Repositories;

public class EfPlayerRepository : IPlayerRepository
{
    private readonly BattleTanksDbContext _context;
    private readonly IDistributedCache _cache;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string CacheById = "player:id:";
    private const string CacheByUser = "player:user:";

    private static readonly Func<BattleTanksDbContext, Guid, Task<Player?>> _getByIdQuery =
        EF.CompileAsyncQuery((BattleTanksDbContext ctx, Guid id) =>
            ctx.Players.AsNoTracking()
               .Include(p => p.User)
               .Include(p => p.GameSession)
               .FirstOrDefault(p => p.Id == id));

    private static readonly Func<BattleTanksDbContext, Guid, Task<Player?>> _getByUserIdQuery =
        EF.CompileAsyncQuery((BattleTanksDbContext ctx, Guid userId) =>
            ctx.Players.AsNoTracking()
               .Include(p => p.User)
               .Include(p => p.GameSession)
               .FirstOrDefault(p => p.UserId == userId));

    public EfPlayerRepository(BattleTanksDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Player?> GetByIdAsync(Guid id)
    {
        var cacheKey = CacheById + id;
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<Player>(cached, _jsonOptions);

        var player = await _getByIdQuery(_context, id);
        if (player != null)
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(player, _jsonOptions),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
            await _cache.SetStringAsync(CacheByUser + player.UserId, JsonSerializer.Serialize(player, _jsonOptions),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
        }
        return player;
    }

    public async Task<Player?> GetByUserIdAsync(Guid userId)
    {
        var cacheKey = CacheByUser + userId;
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<Player>(cached, _jsonOptions);

        var player = await _getByUserIdQuery(_context, userId);
        if (player != null)
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(player, _jsonOptions),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
            await _cache.SetStringAsync(CacheById + player.Id, JsonSerializer.Serialize(player, _jsonOptions),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
        }
        return player;
    }

    public async Task<Player?> GetByConnectionIdAsync(string connectionId)
    {
        return await _context.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.GameSession)
            .FirstOrDefaultAsync(p => p.ConnectionId == connectionId);
    }

    public async Task<List<Player>> GetByGameSessionIdAsync(Guid gameSessionId)
    {
        return await _context.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.GameSessionId == gameSessionId)
            .ToListAsync();
    }

    public async Task<Player?> GetActivePlayerByUserIdAsync(Guid userId)
    {
        return await _context.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.GameSession)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameSessionId != null);
    }

    public async Task AddAsync(Player player)
    {
        await _context.Players.AddAsync(player);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CacheByUser + player.UserId);
        await _cache.RemoveAsync(CacheById + player.Id);
    }

    public async Task UpdateAsync(Player player)
    {
        _context.Players.Update(player);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CacheByUser + player.UserId);
        await _cache.RemoveAsync(CacheById + player.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var player = await _context.Players.FindAsync(id);
        if (player != null)
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(CacheById + id);
            await _cache.RemoveAsync(CacheByUser + player.UserId);
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        var players = await _context.Players
            .Where(p => p.UserId == userId)
            .ToListAsync();

        _context.Players.RemoveRange(players);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CacheByUser + userId);
        foreach (var p in players)
        {
            await _cache.RemoveAsync(CacheById + p.Id);
        }
    }
}