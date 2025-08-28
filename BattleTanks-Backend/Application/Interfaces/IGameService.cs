using Application.DTOs;

namespace Application.Interfaces;

public interface IGameService
{
    Task<RoomStateDto?> CreateRoom(string userId, CreateRoomDto createRoomDto);
    Task<RoomStateDto?> JoinRoom(string userId, string connectionId, JoinRoomDto joinRoomDto);
    Task<bool> LeaveRoom(string userId, string roomId);
    Task<bool> UpdatePlayerPosition(string roomId, string userId, PlayerPositionDto positionDto);
    Task<PlayerStateDto?> GetPlayerPosition(string roomId, string userId);
    Task<bool> MovePlayer(string roomId, string userId, string direction);
    Task<RoomStateDto?> GetRoomState(string roomId);
    Task<RoomStateDto?> GetRoomByCode(string roomCode);
    Task<PlayerStateDto?> GetPlayerState(string roomId, string userId);
    Task<List<RoomStateDto>> GetActiveRooms();
}