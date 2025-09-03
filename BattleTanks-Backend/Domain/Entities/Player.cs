using Domain.ValueObjects;

namespace Domain.Entities;

public class Player
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? GameSessionId { get; private set; }
    public string ConnectionId { get; private set; }
    public Position Position { get; private set; }
    public float Rotation { get; private set; }
    public DateTime LastUpdate { get; private set; }
    public bool IsAlive { get; private set; }
    public int Health { get; private set; }
    public int SessionKills { get; private set; }
    public int SessionDeaths { get; private set; }
    public int SessionScore { get; private set; }
    public bool IsReady { get; private set; }
    
    // Navegación
    public User User { get; private set; }
    public GameSession? GameSession { get; private set; }
    
    private Player() { } // Para EF Core
    
    public static Player Create(Guid userId, string connectionId)
    {
        return new Player
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConnectionId = connectionId,
            Position = Position.Zero,
            Rotation = 0,
            LastUpdate = DateTime.UtcNow,
            IsAlive = true,
            Health = 100,
            SessionKills = 0,
            SessionDeaths = 0,
            SessionScore = 0,
            IsReady = false
        };
    }
    
    public void JoinGameSession(Guid gameSessionId)
    {
        GameSessionId = gameSessionId;
        ResetSessionStats();
        IsReady = false;
    }
    
    public void LeaveGameSession()
    {
        GameSessionId = null;
        ResetSessionStats();
        IsReady = false;
    }
    
    public void UpdatePosition(Position newPosition, float rotation)
    {
        Position = newPosition;
        Rotation = rotation;
        LastUpdate = DateTime.UtcNow;
    }
    
    public void UpdateConnectionId(string connectionId)
    {
        ConnectionId = connectionId;
        LastUpdate = DateTime.UtcNow;
    }
    
    public void TakeDamage(int damage)
    {
        Health = Math.Max(0, Health - damage);
        if (Health == 0)
        {
            IsAlive = false;
            SessionDeaths++;
        }
    }
    
    public void Respawn(Position position)
    {
        Position = position;
        Health = 100;
        IsAlive = true;
        LastUpdate = DateTime.UtcNow;
    }
    
    public void AddKill(int points = 100)
    {
        SessionKills++;
        SessionScore += points;
    }
    
    public void ResetSessionStats()
    {
        SessionKills = 0;
        SessionDeaths = 0;
        SessionScore = 0;
        IsReady = false;
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;
    }
    
    // Propiedades de conveniencia
    public string Username => User?.Username ?? "Unknown";
}