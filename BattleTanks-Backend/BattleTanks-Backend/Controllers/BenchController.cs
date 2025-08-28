using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("bench")]
[AllowAnonymous] // solo para probar sin token
public class BenchController : ControllerBase
{
    private readonly BattleTanksDbContext _db;
    private readonly IScoreRepository _scores;
    private readonly IGameSessionRepository _sessions;
    private readonly IUserRepository _users;

    public BenchController(
        BattleTanksDbContext db,
        IScoreRepository scores,
        IGameSessionRepository sessions,
        IUserRepository users)
    {
        _db = db;
        _scores = scores;
        _sessions = sessions;
        _users = users;
    }

    /// Crea (o devuelve) un usuario y una sesión "bench" para insertar Scores
    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        // Usuario bench (o crea uno)
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == "bench@local");
        if (user == null)
        {
            var userEntity = Domain.Entities.User.Create("bench", "bench@local", "hashed");
            _db.Users.Add(userEntity);     // <-- este era el correcto
            await _db.SaveChangesAsync();
            user = userEntity;             // <-- y reasignas para usar su Id al final
        }

        // Crea una sesión única para cada corrida (más fácil limpiar)
        var session = GameSession.Create($"Bench-{DateTime.UtcNow:HHmmss}", maxPlayers: 4, isPublic: false);
        await _sessions.AddAsync(session);

        return Ok(new { userId = user.Id, sessionId = session.Id, sessionCode = session.Code });
    }

    /// Inserta N scores de forma NAIVE (uno a uno). Parámetro: count (default 10000)
    [HttpPost("insert-scores/naive")]
    public async Task<IActionResult> InsertNaive([FromQuery] int count = 10000, [FromQuery] Guid? userId = null, [FromQuery] Guid? sessionId = null)
    {
        if (userId == null || sessionId == null)
            return BadRequest("Pasa userId y sessionId (usa /bench/setup primero).");

        var sw = Stopwatch.StartNew();

        // Inserta UNO A UNO (peor caso: SaveChanges por item)
        for (int i = 0; i < count; i++)
        {
            var s = Score.Create(userId!.Value, sessionId!.Value,
                                 points: 100 + (i % 200),
                                 kills: i % 7,
                                 deaths: i % 5,
                                 gameDuration: TimeSpan.FromSeconds(180 + (i % 60)),
                                 isWinner: (i % 10 == 0));

            _db.Scores.Add(s);
            await _db.SaveChangesAsync(); // intencionalmente lento
        }

        sw.Stop();
        return Ok(new { method = "naive-one-by-one", inserted = count, ms = sw.ElapsedMilliseconds });
    }

    /// Inserta N scores usando el repositorio (que utiliza BulkInsert). Parámetro: count (default 10000)
    [HttpPost("insert-scores/bulk")]
    public async Task<IActionResult> InsertBulk([FromQuery] int count = 10000, [FromQuery] Guid? userId = null, [FromQuery] Guid? sessionId = null)
    {
        if (userId == null || sessionId == null)
            return BadRequest("Pasa userId y sessionId (usa /bench/setup primero).");

        var list = new List<Score>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(Score.Create(userId!.Value, sessionId!.Value,
                                  points: 100 + (i % 200),
                                  kills: i % 7,
                                  deaths: i % 5,
                                  gameDuration: TimeSpan.FromSeconds(180 + (i % 60)),
                                  isWinner: (i % 10 == 0)));
        }

        var sw = Stopwatch.StartNew();
        await _scores.AddRangeAsync(list); // Usa EFCore.BulkExtensions y si falla, hace fallback
        sw.Stop();

        return Ok(new { method = "bulk-in-repo", inserted = count, ms = sw.ElapsedMilliseconds });
    }

    /// Limpia la sesión (borra la sesión y por cascade borra sus scores)
    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] Guid sessionId)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        if (session == null) return NotFound();

        await _sessions.DeleteAsync(sessionId);
        return Ok(new { deletedSessionId = sessionId });
    }

    /// Cuántos scores hay para una sesión
    [HttpGet("count")]
    public async Task<IActionResult> Count([FromQuery] Guid sessionId)
    {
        var c = await _db.Scores.Where(s => s.GameSessionId == sessionId).CountAsync();
        return Ok(new { sessionId, count = c });
    }
}
