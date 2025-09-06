namespace Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    public int GamesPlayed { get; private set; }
    public int GamesWon { get; private set; }
    public int TotalScore { get; private set; }
    
    private readonly List<Score> _scores = new();
    public IReadOnlyList<Score> Scores => _scores.AsReadOnly();
    
    private User() { }
    
    public static User Create(string username, string email, string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            GamesPlayed = 0,
            GamesWon = 0,
            TotalScore = 0
        };
    }
    
    public void AddGameResult(bool won, int score)
    {
        GamesPlayed++;
        if (won)
        {
            GamesWon++;
        }
        TotalScore += score;
    }
    
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }
    
    public void UpdateEmail(string newEmail)
    {
        Email = newEmail.ToLowerInvariant();
    }
    
    public double WinRate => GamesPlayed > 0 ? (double)GamesWon / GamesPlayed : 0;
}