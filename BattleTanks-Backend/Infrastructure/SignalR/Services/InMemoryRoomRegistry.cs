using System.Collections.Concurrent;
using System.Linq;
using Application.DTOs;
using Application.Interfaces;
using Domain.Enums;
using Domain.Entities;
using Infrastructure.SignalR.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.SignalR.Hubs;

namespace Infrastructure.SignalR.Services;

internal sealed class InMemoryRoomRegistry : IRoomRegistry
{
    private sealed class Room
    {
        public string RoomId { get; init; } = default!;
        public string RoomCode { get; init; } = default!;
        public string Name { get; set; } = "";
        public int MaxPlayers { get; set; }
        public bool IsPublic { get; set; }
        public string Status { get; set; } = GameRoomStatus.Waiting.ToString();
        public ConcurrentDictionary<string, PlayerStateDto> Players { get; } = new();
        public ConcurrentDictionary<(int X, int Y), MapCellDto> MapCells { get; } = new();
        public List<(int X, int Y)> SpawnPoints { get; } = new();
        public int NextSpawnIndex { get; set; } = 0;
        public ConcurrentDictionary<string, (PowerUpDto dto, DateTime spawned)> PowerUps { get; } = new();
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hub;
    private readonly Random _rand = new();
    private readonly ConcurrentDictionary<string, Room> _byId = new();
    private readonly ConcurrentDictionary<string, string> _codeToId = new();
    private readonly ConcurrentDictionary<string, (string roomCode, string userId)> _connIndex = new();

    public InMemoryRoomRegistry(IServiceScopeFactory scopeFactory, IHubContext<GameHub> hub)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _ = RunPowerUpLoop();
    }

    // Create or update room and initialize map if needed
    public Task UpsertRoomAsync(string roomId, string roomCode, string name, int maxPlayers, bool isPublic, string status)
    {
        var room = _byId.AddOrUpdate(
            roomId,
            _ =>
            {
                var created = new Room
                {
                    RoomId = roomId,
                    RoomCode = roomCode,
                    Name = name,
                    MaxPlayers = maxPlayers,
                    IsPublic = isPublic,
                    Status = status
                };
                InitializeMap(created);
                return created;
            },
            (_, r) =>
            {
                r.Name = name;
                r.MaxPlayers = maxPlayers;
                r.IsPublic = isPublic;
                r.Status = status;
                if (r.MapCells.IsEmpty)
                    InitializeMap(r);
                return r;
            }
        );
        _codeToId[roomCode] = roomId;
        return Task.CompletedTask;
    }

    // Get room snapshot by id
    public Task<RoomSnapshot?> GetByIdAsync(string roomId)
    {
        return Task.FromResult(_byId.TryGetValue(roomId, out var r) ? ToSnapshot(r) : null);
    }

    // Get room snapshot by code
    public Task<RoomSnapshot?> GetByCodeAsync(string roomCode)
    {
        if (_codeToId.TryGetValue(roomCode, out var id) && _byId.TryGetValue(id, out var r))
            return Task.FromResult<RoomSnapshot?>(ToSnapshot(r));
        return Task.FromResult<RoomSnapshot?>(null);
    }

    // Add player to room
    public async Task JoinAsync(string roomCode, string userId, string username, string connectionId)
    {
        var room = await EnsureRoomByCodeAsync(roomCode);
        if (room.Status != GameRoomStatus.Waiting.ToString())
            throw new InvalidOperationException("room_already_started");

        var state = new PlayerStateDto(userId, username, 0, 0, 0, 3, true, 0, false, 200);
        room.Players[userId] = state;
        _connIndex[connectionId] = (roomCode, userId);
    }

    // Remove player by connection
    public Task<(string? roomCode, string? userId)> LeaveByConnectionAsync(string connectionId)
    {
        if (!_connIndex.TryRemove(connectionId, out var info))
            return Task.FromResult<(string?, string?)>((null, null));

        if (_codeToId.TryGetValue(info.roomCode, out var roomId) && _byId.TryGetValue(roomId, out var room))
            room.Players.TryRemove(info.userId, out _);

        return Task.FromResult((info.roomCode, info.userId));
    }

    // Get all players in a room
    public Task<IReadOnlyCollection<PlayerStateDto>> GetPlayersByIdAsync(string roomId)
    {
        if (_byId.TryGetValue(roomId, out var r))
            return Task.FromResult<IReadOnlyCollection<PlayerStateDto>>(r.Players.Values.ToArray());
        return Task.FromResult<IReadOnlyCollection<PlayerStateDto>>(Array.Empty<PlayerStateDto>());
    }

    public Task UpdatePlayerStatsAsync(string roomCode, string playerId, int lives, int score, bool isAlive)
    {
        if (_codeToId.TryGetValue(roomCode, out var roomId) && _byId.TryGetValue(roomId, out var room))
        {
            if (room.Players.TryGetValue(playerId, out var state))
            {
                room.Players[playerId] = state with { Lives = lives, Score = score, IsAlive = isAlive };
            }
        }
        return Task.CompletedTask;
    }

    public async Task SetPlayerReadyAsync(string roomCode, string playerId, bool ready)
    {
        if (_codeToId.TryGetValue(roomCode, out var roomId) && _byId.TryGetValue(roomId, out var room))
        {
            if (room.Players.TryGetValue(playerId, out var state))
            {
                room.Players[playerId] = state with { IsReady = ready };
                await _hub.Clients.Group(room.RoomCode).SendAsync("playerReady", new { userId = playerId, ready });

                if (room.Status == GameRoomStatus.Waiting.ToString() &&
                    room.Players.Count >= 2 &&
                    room.Players.Values.All(p => p.IsReady))
                {
                    ResetMap(room);
                    room.Status = GameRoomStatus.InProgress.ToString();

                    // Persist room status change so HTTP queries reflect it
                    using var scope = _scopeFactory.CreateScope();
                    var sessions = scope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
                    var session = await sessions.GetByCodeAsync(room.RoomCode);
                    if (session != null)
                    {
                        // Mark players ready so domain StartGame passes validation
                        foreach (var p in session.Players)
                            p.SetReady(true);

                        session.StartGame();
                        await sessions.UpdateAsync(session);
                    }

                    await _hub.Clients.Group(room.RoomCode).SendAsync("gameStarted");
                }
            }
        }
    }

    // Ensure room exists and initialize map if missing
    private async Task<Room> EnsureRoomByCodeAsync(string roomCode)
    {
        if (_codeToId.TryGetValue(roomCode, out var id) && _byId.TryGetValue(id, out var cached))
        {
            if (cached.MapCells.IsEmpty)
                InitializeMap(cached);
            return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var session = await sessions.GetByCodeAsync(roomCode) ?? throw new InvalidOperationException("room_not_found");

        var room = _byId.GetOrAdd(session.Id.ToString(), _ =>
            new Room
            {
                RoomId = session.Id.ToString(),
                RoomCode = session.Code,
                Name = session.Name,
                MaxPlayers = session.MaxPlayers,
                IsPublic = session.IsPublic,
                Status = session.Status.ToString()
            });

        if (room.MapCells.IsEmpty)
            InitializeMap(room);

        _codeToId[room.RoomCode] = room.RoomId;
        return room;
    }

    private static RoomSnapshot ToSnapshot(Room r) =>
        new RoomSnapshot(
            r.RoomId,
            r.RoomCode,
            r.Name,
            r.MaxPlayers,
            r.IsPublic,
            r.Status,
            r.Players
        );

    private static void ResetMap(Room room)
    {
        room.MapCells.Clear();
        room.SpawnPoints.Clear();
        room.PowerUps.Clear();
        room.NextSpawnIndex = 0;
        InitializeMap(room);
    }

    // Initialize default map and spawn points
    private static void InitializeMap(Room room)
    {
        if (!room.MapCells.IsEmpty)
            return;

        int[,] layout = new int[20, 20]
        {
            {2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2},
            {2,3,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,3,2},
            {2,0,1,0,1,0,1,0,0,0,0,0,0,1,0,1,0,1,0,2},
            {2,0,1,0,1,0,1,0,0,2,2,0,0,1,0,1,0,1,0,2},
            {2,0,1,0,1,0,1,0,0,2,2,0,0,1,0,1,0,1,0,2},
            {2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2},
            {2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2},
            {2,0,0,1,1,1,0,0,1,1,1,1,0,0,1,1,1,0,0,2},
            {2,0,0,1,2,1,0,0,1,2,2,1,0,0,1,2,1,0,0,2},
            {2,0,0,1,2,1,0,0,1,2,2,1,0,0,1,2,1,0,0,2},
            {2,0,0,1,1,1,0,0,1,1,1,1,0,0,1,1,1,0,0,2},
            {2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2},
            {2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2},
            {2,0,1,0,1,0,1,0,0,0,0,0,0,1,0,1,0,1,0,2},
            {2,0,1,0,1,0,1,0,0,0,0,0,0,1,0,1,0,1,0,2},
            {2,0,1,0,1,0,1,0,0,1,1,0,0,1,0,1,0,1,0,2},
            {2,0,0,0,0,0,0,0,0,1,1,0,0,0,0,0,0,0,0,2},
            {2,0,0,1,1,1,0,0,0,1,1,0,0,0,1,1,1,0,0,2},
            {2,0,0,1,3,1,0,0,0,1,1,0,0,0,1,3,1,0,0,2},
            {2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2}
        };

        for (int y = 0; y < layout.GetLength(0); y++)
        {
            for (int x = 0; x < layout.GetLength(1); x++)
            {
                int val = layout[y, x];
                bool destructible = val == 1 || val == 3;
                room.MapCells[(x, y)] = new MapCellDto(x, y, val, destructible, false);
            }
        }

        room.SpawnPoints.Clear();
        room.SpawnPoints.Add((1, 1));
        room.SpawnPoints.Add((18, 1));
        room.SpawnPoints.Add((9, 18));
        room.SpawnPoints.Add((10, 18));
        room.NextSpawnIndex = 0;
    }

    // Return full map for a room
    public Task<MapCellDto[]> GetMapAsync(string roomId)
    {
        if (_byId.TryGetValue(roomId, out var room))
        {
            if (room.MapCells.IsEmpty)
                InitializeMap(room);
            return Task.FromResult(room.MapCells.Values.ToArray());
        }
        return Task.FromResult(Array.Empty<MapCellDto>());
    }

    // Destroy cell if destructible
    public Task<MapCellDto?> DestroyCellAsync(string roomId, int x, int y)
    {
        if (_byId.TryGetValue(roomId, out var room))
        {
            if (room.MapCells.TryGetValue((x, y), out var cell))
            {
                if (cell.IsDestructible && !cell.IsDestroyed)
                {
                    var updated = cell with { IsDestroyed = true };
                    room.MapCells[(x, y)] = updated;
                    return Task.FromResult<MapCellDto?>(updated);
                }
            }
        }
        return Task.FromResult<MapCellDto?>(null);
    }

    // Allocate spawn point in round-robin order
    public Task<(int X, int Y)> AllocateSpawnAsync(string roomId, string userId)
    {
        if (_byId.TryGetValue(roomId, out var room) && room.SpawnPoints.Count > 0)
        {
            var idx = room.NextSpawnIndex % room.SpawnPoints.Count;
            var coord = room.SpawnPoints[idx];
            room.NextSpawnIndex = (idx + 1) % room.SpawnPoints.Count;
            return Task.FromResult(coord);
        }
        return Task.FromResult((0, 0));
    }

    public IEnumerable<(string RoomId, string RoomCode)> ListRooms() =>
        _byId.Values.Select(r => (r.RoomId, r.RoomCode));

    public Task<IReadOnlyCollection<PowerUpDto>> GetPowerUpsAsync(string roomId)
    {
        if (_byId.TryGetValue(roomId, out var room))
            return Task.FromResult<IReadOnlyCollection<PowerUpDto>>(room.PowerUps.Values.Select(p => p.dto).ToArray());
        return Task.FromResult<IReadOnlyCollection<PowerUpDto>>(Array.Empty<PowerUpDto>());
    }

    public Task<PowerUpDto?> RemovePowerUpAsync(string roomId, string powerUpId)
    {
        if (_byId.TryGetValue(roomId, out var room) && room.PowerUps.TryRemove(powerUpId, out var state))
            return Task.FromResult<PowerUpDto?>(state.dto);
        return Task.FromResult<PowerUpDto?>(null);
    }

    public Task<PowerUpDto?> SpawnPowerUpAsync(string roomId)
    {
        if (!_byId.TryGetValue(roomId, out var room))
            return Task.FromResult<PowerUpDto?>(null);

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int cx = _rand.Next(0, 20);
            int cy = _rand.Next(0, 20);
            if (room.MapCells.TryGetValue((cx, cy), out var cell) && cell.Type == 0)
            {
                var type = _rand.Next(0, 2) == 0 ? PowerUpType.Shield : PowerUpType.Speed;
                var id = Guid.NewGuid().ToString();
                float px = (cx + 0.5f) * 40f;
                float py = (cy + 0.5f) * 40f;
                var dto = new PowerUpDto(id, type.ToString().ToLower(), px, py);
                room.PowerUps[id] = (dto, DateTime.UtcNow);
                return Task.FromResult<PowerUpDto?>(dto);
            }
        }
        return Task.FromResult<PowerUpDto?>(null);
    }

    public Task<IReadOnlyList<string>> RemoveExpiredPowerUpsAsync(string roomId, TimeSpan lifetime)
    {
        if (!_byId.TryGetValue(roomId, out var room))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        List<string> removed = new();
        var now = DateTime.UtcNow;
        foreach (var kv in room.PowerUps.ToArray())
        {
            if (now - kv.Value.spawned >= lifetime)
            {
                if (room.PowerUps.TryRemove(kv.Key, out _))
                    removed.Add(kv.Key);
            }
        }
        return Task.FromResult<IReadOnlyList<string>>(removed);
    }

    public Task<PlayerStateDto?> GetPlayerStateAsync(string roomCode, string playerId)
    {
        if (_codeToId.TryGetValue(roomCode, out var roomId) && _byId.TryGetValue(roomId, out var room))
        {
            if (room.Players.TryGetValue(playerId, out var state))
                return Task.FromResult<PlayerStateDto?>(state);
        }
        return Task.FromResult<PlayerStateDto?>(null);
    }

    public Task SetPlayerPowerUpAsync(string roomCode, string playerId, bool? hasShield = null, float? speed = null)
    {
        if (_codeToId.TryGetValue(roomCode, out var roomId) && _byId.TryGetValue(roomId, out var room))
        {
            if (room.Players.TryGetValue(playerId, out var state))
            {
                var updated = state with
                {
                    HasShield = hasShield ?? state.HasShield,
                    Speed = speed ?? state.Speed
                };
                room.Players[playerId] = updated;
            }
        }
        return Task.CompletedTask;
    }

    private async Task RunPowerUpLoop()
    {
        var lifetime = TimeSpan.FromSeconds(10);
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            foreach (var room in _byId.Values)
            {
                var expired = await RemoveExpiredPowerUpsAsync(room.RoomId, lifetime);
                foreach (var id in expired)
                {
                    await _hub.Clients.Group(room.RoomCode).SendAsync("powerUpRemoved", id);
                }
                if (room.PowerUps.Count == 0)
                {
                    var spawned = await SpawnPowerUpAsync(room.RoomId);
                    if (spawned != null)
                        await _hub.Clients.Group(room.RoomCode).SendAsync("powerUpSpawned", spawned);
                }
            }
        }
    }
}
