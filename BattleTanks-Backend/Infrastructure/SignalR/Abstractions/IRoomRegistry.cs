using Application.DTOs;

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

    // Get map snapshot
    Task<MapCellDto[]> GetMapAsync(string roomId);

    // Destroy a map cell
    Task<MapCellDto?> DestroyCellAsync(string roomId, int x, int y);

    // Allocate spawn position
    Task<(int X, int Y)> AllocateSpawnAsync(string roomId, string userId);
}
