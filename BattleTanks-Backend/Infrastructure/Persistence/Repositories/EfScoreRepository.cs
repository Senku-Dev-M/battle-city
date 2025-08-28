using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using StackExchange.Redis;

namespace Infrastructure.Persistence.Repositories;

public class EfScoreRepository : IScoreRepository
{
    private readonly BattleTanksDbContext _context;
    private readonly StackExchange.Redis.IDatabase? _redisDb;

    public EfScoreRepository(BattleTanksDbContext context, StackExchange.Redis.IConnectionMultiplexer? redis = null)
    {
        _context = context;
        _redisDb = redis?.GetDatabase();
    }

    public async Task<Score?> GetByIdAsync(Guid id)
    {
        return await _context.Scores
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.GameSession)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Score>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Scores
            .AsNoTracking()
            .Include(s => s.GameSession)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.AchievedAt)
            .ToListAsync();
    }

    public async Task<List<Score>> GetByGameSessionIdAsync(Guid gameSessionId)
    {
        return await _context.Scores
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.GameSessionId == gameSessionId)
            .OrderByDescending(s => s.Points)
            .ToListAsync();
    }

    public async Task<List<Score>> GetTopScoresAsync(int limit = 10)
    {
        // Intentar obtener del caché de Redis
        if (_redisDb != null)
        {
            var cached = await _redisDb.StringGetAsync($"scores:top:{limit}");
            if (cached.HasValue)
            {
                try
                {
                    var scores = System.Text.Json.JsonSerializer.Deserialize<List<Score>>(cached!) ?? new List<Score>();
                    return scores;
                }
                catch
                {
                    // en caso de error de deserialización, continuar con DB
                }
            }
        }

        var result = await _context.Scores
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.GameSession)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.AchievedAt)
            .Take(limit)
            .ToListAsync();

        // Guardar en caché por 5 minutos
        if (_redisDb != null)
        {
            try
            {
                var serialized = System.Text.Json.JsonSerializer.Serialize(result);
                await _redisDb.StringSetAsync($"scores:top:{limit}", serialized, TimeSpan.FromMinutes(5));
            }
            catch
            {
                // Ignorar errores de serialización / Redis
            }
        }
        return result;
    }

    public async Task<List<Score>> GetUserTopScoresAsync(Guid userId, int limit = 10)
    {
        return await _context.Scores
            .AsNoTracking()
            .Include(s => s.GameSession)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.AchievedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task AddAsync(Score score)
    {
        await _context.Scores.AddAsync(score);
        await _context.SaveChangesAsync();

        // Invalidar ranking en caché
        if (_redisDb != null)
        {
            try
            {
                // Eliminar todas las claves de ranking conocidas (por ejemplo top 10)
                await _redisDb.KeyDeleteAsync("scores:top:10");
            }
            catch { }
        }
    }

    public async Task AddRangeAsync(IEnumerable<Score> scores)
    {
        // Utilizar operaciones masivas para insertar múltiples registros de forma eficiente
        var list = scores.ToList();
        // Si la biblioteca BulkExtensions está disponible, usar BulkInsert; de lo contrario, fallback a AddRangeAsync
        try
        {
            await _context.BulkInsertAsync(list);
        }
        catch
        {
            await _context.Scores.AddRangeAsync(list);
            await _context.SaveChangesAsync();
        }

        // Invalidar ranking en caché
        if (_redisDb != null)
        {
            try
            {
                await _redisDb.KeyDeleteAsync("scores:top:10");
            }
            catch { }
        }
    }

    public async Task UpdateAsync(Score score)
    {
        _context.Scores.Update(score);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var score = await _context.Scores.FindAsync(id);
        if (score != null)
        {
            _context.Scores.Remove(score);
            await _context.SaveChangesAsync();
        }
    }
}