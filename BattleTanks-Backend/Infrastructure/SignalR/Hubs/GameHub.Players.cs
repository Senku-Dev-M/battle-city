using Application.DTOs;
using Infrastructure.SignalR.Abstractions;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Claims;
using Infrastructure.Interfaces;

namespace Infrastructure.SignalR.Hubs;

public partial class GameHub : Hub
{
    private readonly IConnectionTracker _tracker;
    private readonly IRoomRegistry _rooms;
    private readonly IMqttService _mqtt;
    private readonly IEventHistoryService _history;

    public GameHub(IConnectionTracker tracker, IRoomRegistry rooms, IMqttService mqtt, IEventHistoryService history)
    {
        _tracker = tracker;
        _rooms = rooms;
        _mqtt = mqtt;
        _history = history;
    }

    private static readonly (int x, int y)[] _spawnPoints = new[]
    {
        (1, 1),
        (18, 1),
        (9, 18),
        (10, 18)
    };

    private static readonly ConcurrentDictionary<string, int> _spawnIndexByRoom = new();

    // Join a room, initialize player, broadcast state
    public async Task JoinRoom(string roomCode, string? username = null, string? joinKey = null)
    {
        var userId = Context.User?.FindFirst("user_id")?.Value
                     ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Guid.NewGuid().ToString();

        var uname = Context.User?.FindFirst("username")?.Value
                    ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
                    ?? (string.IsNullOrWhiteSpace(username) ? $"Player-{userId[..8]}" : username.Trim());

        try
        {
            await _rooms.JoinAsync(roomCode, userId, uname, Context.ConnectionId);
        }
        catch
        {
            throw new HubException("room_not_found");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        _tracker.Set(Context.ConnectionId, (await _rooms.GetByCodeAsync(roomCode))!.RoomId, roomCode, userId, uname);

        if (!_playerLivesByRoom.ContainsKey(roomCode))
            _playerLivesByRoom[roomCode] = new();
        _playerLivesByRoom[roomCode][userId] = 3;

        if (!_playerScoresByRoom.ContainsKey(roomCode))
            _playerScoresByRoom[roomCode] = new();
        _playerScoresByRoom[roomCode][userId] = 0;

        await Clients.Group(roomCode).SendAsync("playerJoined", new { userId, username = uname });
        // Publish join event via MQTT and record in history
        try
        {
            await _mqtt.PublishAsync($"game/{roomCode}/events/playerJoined", new { userId, username = uname });
            await _history.AddEventAsync(roomCode, "playerJoined", new { userId, username = uname });
        }
        catch
        {
            // Swallow exceptions from MQTT/history so that join succeeds even if broker is unavailable
        }

        var snap = await _rooms.GetByCodeAsync(roomCode);
        if (snap is not null)
        {
            await Clients.Caller.SendAsync("roomSnapshot", new
            {
                roomId = snap.RoomId,
                roomCode = snap.RoomCode,
                players = snap.Players.Values.ToArray()
            });
        }

        var spawnIndex = _spawnIndexByRoom.AddOrUpdate(roomCode, 0, (_, current) => (current + 1) % _spawnPoints.Length);
        var spawn = _spawnPoints[spawnIndex];
        float spawnX = (spawn.x + 0.5f) * 40f;
        float spawnY = (spawn.y + 0.5f) * 40f;

        var initialState = new
        {
            playerId = userId,
            username = uname,
            x = spawnX,
            y = spawnY,
            rotation = 0f,
            lives = 3,
            score = 0,
            isAlive = true,
            hasShield = false,
            speed = 200f
        };
        await Clients.Group(roomCode).SendAsync("playerMoved", initialState);
        // Broadcast initial spawn over MQTT and record history
        try
        {
            await _mqtt.PublishAsync($"game/{roomCode}/events/playerMoved", initialState);
            await _history.AddEventAsync(roomCode, "playerMoved", initialState);
        }
        catch { }

        // Send recent event history to the newly joined client
        try
        {
            var history = await _history.GetEventsAsync(roomCode, 50);
            if (history.Count > 0)
            {
                await Clients.Caller.SendAsync("eventHistory", history);
            }
        }
        catch { }
    }

    // Leave room and notify clients
    public async Task LeaveRoom()
    {
        var left = await _rooms.LeaveByConnectionAsync(Context.ConnectionId);
        if (!string.IsNullOrEmpty(left.roomCode) && !string.IsNullOrEmpty(left.userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, left.roomCode!);
            _tracker.Remove(Context.ConnectionId);

            if (_playerLivesByRoom.TryGetValue(left.roomCode!, out var roomLives))
            {
                roomLives.TryRemove(left.userId!, out _);
            }
            if (_playerScoresByRoom.TryGetValue(left.roomCode!, out var roomScores))
            {
                roomScores.TryRemove(left.userId!, out _);
            }

            await Clients.Group(left.roomCode!).SendAsync("playerLeft", left.userId!);
            // Broadcast leave over MQTT and record history
            try
            {
                await _mqtt.PublishAsync($"game/{left.roomCode}/events/playerLeft", new { userId = left.userId });
                await _history.AddEventAsync(left.roomCode!, "playerLeft", new { userId = left.userId });
            }
            catch { }
        }
    }

    // Handle disconnection cleanup
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try { await LeaveRoom(); } catch { }
        await base.OnDisconnectedAsync(exception);
    }

    // Update player position and broadcast
    public async Task UpdatePosition(PlayerPositionDto position)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");

        if (_playerLivesByRoom.TryGetValue(info.RoomCode, out var livesDict)
            && livesDict.TryGetValue(info.UserId, out var lives)
            && lives <= 0)
        {
            return;
        }

        if (float.IsNaN(position.X) || float.IsInfinity(position.X) ||
            float.IsNaN(position.Y) || float.IsInfinity(position.Y) ||
            float.IsNaN(position.Rotation) || float.IsInfinity(position.Rotation))
            return;

        var fixedDto = new PlayerPositionDto(
            info.UserId,
            position.X,
            position.Y,
            position.Rotation,
            position.Timestamp > 0 ? position.Timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        await Clients.Group(info.RoomCode).SendAsync("playerMoved", fixedDto);
        // Publish movement update via MQTT and record history
        try
        {
            await _mqtt.PublishAsync($"game/{info.RoomCode}/events/playerMoved", fixedDto);
            await _history.AddEventAsync(info.RoomCode, "playerMoved", fixedDto);
        }
        catch { }
    }
}
