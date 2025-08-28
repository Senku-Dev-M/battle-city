using System.Collections.Concurrent;
using Application.DTOs;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.SignalR.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, Room> _byId = new();
    private readonly ConcurrentDictionary<string, string> _codeToId = new();
    private readonly ConcurrentDictionary<string, (string roomCode, string userId)> _connIndex = new();

    public InMemoryRoomRegistry(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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
        var state = new PlayerStateDto(userId, username, 0, 0, 0, 5, true);
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
                bool destructible = val == 1;
                room.MapCells[(x, y)] = new MapCellDto(x, y, destructible, false);
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
}
