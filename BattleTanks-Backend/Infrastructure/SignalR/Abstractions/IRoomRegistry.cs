using Application.DTOs;
using System;

namespace Infrastructure.SignalR.Abstractions;

public record RoomSnapshot(
    string RoomId,
    string RoomCode,
    string Name,
    int MaxPlayers,
    bool IsPublic,
    string Status,
    IReadOnlyDictionary<string, PlayerStateDto> Players
);

public interface IRoomRegistry
{
    // Insert or update a room
    Task UpsertRoomAsync(string roomId, string roomCode, string name, int maxPlayers, bool isPublic, string status);

    // Get room by id
    Task<RoomSnapshot?> GetByIdAsync(string roomId);

    // Get room by code
    Task<RoomSnapshot?> GetByCodeAsync(string roomCode);

    // Join a room
    Task JoinAsync(string roomCode, string userId, string username, string connectionId);

    // Leave by connection
    Task<(string? roomCode, string? userId)> LeaveByConnectionAsync(string connectionId);

    // Get players in a room
    Task<IReadOnlyCollection<PlayerStateDto>> GetPlayersByIdAsync(string roomId);

    // Update a player's lives/score
    Task UpdatePlayerStatsAsync(string roomCode, string playerId, int lives, int score, bool isAlive);

    // Update ready status
    Task SetPlayerReadyAsync(string roomCode, string playerId, bool ready);

    // Get map snapshot
    Task<MapCellDto[]> GetMapAsync(string roomId);

    // Destroy a map cell
    Task<MapCellDto?> DestroyCellAsync(string roomId, int x, int y);

    // Allocate spawn position
    Task<(int X, int Y)> AllocateSpawnAsync(string roomId, string userId);

    // Power-ups
    IEnumerable<(string RoomId, string RoomCode)> ListRooms();
    Task<IReadOnlyCollection<PowerUpDto>> GetPowerUpsAsync(string roomId);
    Task<PowerUpDto?> RemovePowerUpAsync(string roomId, string powerUpId);
    Task<PowerUpDto?> SpawnPowerUpAsync(string roomId);
    Task<IReadOnlyList<string>> RemoveExpiredPowerUpsAsync(string roomId, TimeSpan lifetime);
    Task<PlayerStateDto?> GetPlayerStateAsync(string roomCode, string playerId);
    Task SetPlayerPowerUpAsync(string roomCode, string playerId, bool? hasShield = null, float? speed = null);
}
