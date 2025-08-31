using System.Threading.Tasks;
using Application.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.SignalR.Hubs;

public partial class GameHub : Hub
{
    public async Task GetPowerUps()
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");
        var list = await _rooms.GetPowerUpsAsync(info.RoomId);
        await Clients.Caller.SendAsync("powerUpState", list);
    }

    public async Task CollectPowerUp(string powerUpId)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");
        var removed = await _rooms.RemovePowerUpAsync(info.RoomId, powerUpId);
        if (removed == null) return;
        await Clients.Group(info.RoomCode).SendAsync("powerUpRemoved", powerUpId);
        if (removed.Type == PowerUpType.Shield.ToString().ToLower())
        {
            await _rooms.SetPlayerPowerUpAsync(info.RoomCode, info.UserId, hasShield: true);
            var state = await _rooms.GetPlayerStateAsync(info.RoomCode, info.UserId);
            if (state != null)
                await Clients.Group(info.RoomCode).SendAsync("playerMoved", state);
        }
        else if (removed.Type == PowerUpType.Speed.ToString().ToLower())
        {
            await _rooms.SetPlayerPowerUpAsync(info.RoomCode, info.UserId, speed: 400);
            var state = await _rooms.GetPlayerStateAsync(info.RoomCode, info.UserId);
            if (state != null)
                await Clients.Group(info.RoomCode).SendAsync("playerMoved", state);
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                await _rooms.SetPlayerPowerUpAsync(info.RoomCode, info.UserId, speed: 200);
                var state2 = await _rooms.GetPlayerStateAsync(info.RoomCode, info.UserId);
                if (state2 != null)
                    await Clients.Group(info.RoomCode).SendAsync("playerMoved", state2);
            });
        }
    }
}
