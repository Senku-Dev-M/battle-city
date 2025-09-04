using System;
using System.Collections.Concurrent;
using System.Linq;
using Application.DTOs;
using Microsoft.AspNetCore.SignalR;
using Domain.Enums;

namespace Infrastructure.SignalR.Hubs;

public partial class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, BulletStateDto>> _bulletsByRoom = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _playerLivesByRoom = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _playerScoresByRoom = new();

    // Spawn a bullet with cooldown and lifetime checks
    public async Task<string> SpawnBullet(float x, float y, float rotation, float speed)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info)) throw new HubException("not_in_room");

        if (_playerLivesByRoom.TryGetValue(info.RoomCode, out var livesDict)
            && livesDict.TryGetValue(info.UserId, out var lives)
            && lives <= 0)
        {
            return string.Empty;
        }

        var normalizedRotation = NormalizeAngle(rotation);
        var clampedSpeed = Clamp(speed, 0.1f, 100f);

        const long BULLET_LIFETIME_MS = 2_000;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_bulletsByRoom.TryGetValue(info.RoomCode, out var existingBullets))
        {
            List<string> expired = new();
            foreach (var kvp in existingBullets)
            {
                var b = kvp.Value;
                var age = now - b.SpawnTimestamp;
                if (age >= BULLET_LIFETIME_MS)
                {
                    expired.Add(kvp.Key);
                    continue;
                }
                if (b.IsActive && b.ShooterId == info.UserId)
                {
                    return string.Empty;
                }
            }
            foreach (var id in expired)
            {
                if (existingBullets.TryRemove(id, out var removed))
                {
                    await Clients.Group(info.RoomCode).SendAsync("bulletDespawned", id, "timeout");
                }
            }
        }

        var bulletId = Guid.NewGuid().ToString();
        var state = new BulletStateDto(
            bulletId,
            info.RoomId,
            info.UserId,
            x,
            y,
            normalizedRotation,
            clampedSpeed,
            now,
            true
        );

        var roomDict = _bulletsByRoom.GetOrAdd(info.RoomCode, _ => new ConcurrentDictionary<string, BulletStateDto>());
        roomDict[bulletId] = state;
        await Clients.Group(info.RoomCode).SendAsync("bulletSpawned", state);
        // Publish bullet spawn event and record history
        try
        {
            await _mqtt.PublishAsync($"game/{info.RoomCode}/events/bulletSpawned", state);
            await _history.AddEventAsync(info.RoomCode, "bulletSpawned", state);
        }
        catch { }
        return bulletId;
    }

    // Report a hit and reduce target lives
    public async Task ReportHit(BulletHitReportDto dto)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info)) throw new HubException("not_in_room");
        var roomCode = info.RoomCode;

        if (!_bulletsByRoom.TryGetValue(roomCode, out var roomBullets)) return;
        if (!roomBullets.TryGetValue(dto.BulletId, out var bullet) || !bullet.IsActive) return;

        var updated = bullet with { IsActive = false };
        roomBullets[dto.BulletId] = updated;

        var targetState = await _rooms.GetPlayerStateAsync(roomCode, dto.TargetPlayerId);
        if (targetState?.HasShield == true)
        {
            await _rooms.SetPlayerPowerUpAsync(roomCode, dto.TargetPlayerId, hasShield: false);
            roomBullets.TryRemove(dto.BulletId, out _);
            await Clients.Group(roomCode).SendAsync("bulletDespawned", dto.BulletId, "shield");
            var newState = targetState with { HasShield = false };
            await Clients.Group(roomCode).SendAsync("playerMoved", newState);
            return;
        }

        if (_playerLivesByRoom.TryGetValue(roomCode, out var roomLives))
        {
            if (roomLives.TryGetValue(dto.TargetPlayerId, out var lives))
            {
                var newLives = Math.Max(0, lives - 1);
                roomLives[dto.TargetPlayerId] = newLives;

                if (!_playerScoresByRoom.TryGetValue(roomCode, out var roomScores))
                {
                    roomScores = new ConcurrentDictionary<string, int>();
                    _playerScoresByRoom[roomCode] = roomScores;
                }

                roomScores.TryGetValue(updated.ShooterId, out var shooterScore);
                if (newLives <= 0)
                {
                    shooterScore = roomScores.AddOrUpdate(updated.ShooterId, 1, (_, s) => s + 1);
                }

                // update registry
                await _rooms.UpdatePlayerStatsAsync(roomCode, dto.TargetPlayerId, newLives,
                    roomScores.TryGetValue(dto.TargetPlayerId, out var targetScore) ? targetScore : 0,
                    newLives > 0);
                await _rooms.UpdatePlayerStatsAsync(roomCode, updated.ShooterId,
                    roomLives.TryGetValue(updated.ShooterId, out var shooterLives) ? shooterLives : 0,
                    shooterScore, true);

                // Notify clients via SignalR
                await Clients.Group(roomCode).SendAsync("bulletDespawned", dto.BulletId, "hit");
                // Publish via MQTT and record history
                try
                {
                    await _mqtt.PublishAsync($"game/{roomCode}/events/bulletDespawned", new { bulletId = dto.BulletId, reason = "hit" });
                    await _history.AddEventAsync(roomCode, "bulletDespawned", new { bulletId = dto.BulletId, reason = "hit" });
                }
                catch { }

                var playerHit = new PlayerHitDto(
                    dto.BulletId,
                    dto.TargetPlayerId,
                    updated.ShooterId,
                    1,
                    newLives,
                    newLives > 0,
                    shooterScore
                );
                await Clients.Group(roomCode).SendAsync("playerHit", playerHit);
                try
                {
                    await _mqtt.PublishAsync($"game/{roomCode}/events/playerHit", playerHit);
                    await _history.AddEventAsync(roomCode, "playerHit", playerHit);
                }
                catch { }

                if (newLives <= 0)
                {
                    await Clients.Group(roomCode).SendAsync("playerDied", dto.TargetPlayerId);
                    try
                    {
                        await _mqtt.PublishAsync($"game/{roomCode}/events/playerDied", new { playerId = dto.TargetPlayerId });
                        await _history.AddEventAsync(roomCode, "playerDied", new { playerId = dto.TargetPlayerId });
                    }
                    catch { }

                    await CheckForGameOver(roomCode);
                }
            }
        }
    }

    // Report bullet collision with obstacle
    public async Task ReportObstacleHit(string bulletId)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info)) throw new HubException("not_in_room");
        var roomCode = info.RoomCode;

        if (!_bulletsByRoom.TryGetValue(roomCode, out var roomBullets)) return;
        if (!roomBullets.TryGetValue(bulletId, out var bullet) || !bullet.IsActive) return;

        var updated = bullet with { IsActive = false };
        roomBullets[bulletId] = updated;
        roomBullets.TryRemove(bulletId, out _);

        await Clients.Group(roomCode).SendAsync("bulletDespawned", bulletId, "block");
        // Publish block despawn and record history
        try
        {
            await _mqtt.PublishAsync($"game/{roomCode}/events/bulletDespawned", new { bulletId, reason = "block" });
            await _history.AddEventAsync(roomCode, "bulletDespawned", new { bulletId, reason = "block" });
        }
        catch { }
    }

    private async Task CheckForGameOver(string roomCode)
    {
        if (!_playerLivesByRoom.TryGetValue(roomCode, out var roomLives))
            return;

        var alive = roomLives.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (alive.Count > 1)
            return;

        var snapshot = await _rooms.GetByCodeAsync(roomCode);
        if (snapshot == null || snapshot.Status != GameRoomStatus.InProgress.ToString())
            return;

        var winnerId = alive.Count == 1 ? alive[0] : null;

        await _rooms.UpsertRoomAsync(
            snapshot.RoomId,
            snapshot.RoomCode,
            snapshot.Name,
            snapshot.MaxPlayers,
            snapshot.IsPublic,
            GameRoomStatus.Finished.ToString());

        if (Guid.TryParse(snapshot.RoomId, out var roomGuid))
        {
            await _sessions.UpdateStatusAsync(roomGuid, GameRoomStatus.Finished);
        }

        // Notify clients of game end and individual match results before cleaning up state
        await Clients.Group(roomCode).SendAsync("gameFinished", winnerId);

        foreach (var kv in roomLives)
        {
            var connId = _tracker.GetConnectionIdByUserId(kv.Key);
            if (connId != null)
            {
                var didWin = kv.Value > 0;
                await Clients.Client(connId).SendAsync("matchResult", didWin);
            }
        }

        _playerLivesByRoom.TryRemove(roomCode, out _);
        _playerScoresByRoom.TryRemove(roomCode, out _);
        _bulletsByRoom.TryRemove(roomCode, out _);

        try
        {
            await _mqtt.PublishAsync($"game/{roomCode}/events/gameFinished", new { winnerId });
            await _history.AddEventAsync(roomCode, "gameFinished", new { winnerId });
        }
        catch { }
    }
}
