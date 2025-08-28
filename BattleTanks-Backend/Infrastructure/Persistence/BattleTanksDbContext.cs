using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class BattleTanksDbContext : DbContext
{
    public BattleTanksDbContext(DbContextOptions<BattleTanksDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Score> Scores { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();

            entity.Property(e => e.GamesPlayed).HasDefaultValue(0);
            entity.Property(e => e.GamesWon).HasDefaultValue(0);
            entity.Property(e => e.TotalScore).HasDefaultValue(0);

            // Índice compuesto para ranking por puntuación total y partidas ganadas
            entity.HasIndex(e => new { e.TotalScore, e.GamesWon });
        });

        // GameSession
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.IsPublic).IsRequired().HasDefaultValue(true);

            // Región: necesaria para el sharding por ubicación/región. Valor por defecto "default".
            entity.Property(e => e.Region).IsRequired().HasMaxLength(50).HasDefaultValue("default");

            // Índice compuesto para optimizar consultas filtradas por estado, visibilidad y fecha de creación.
            // Esto mejora el rendimiento al obtener sesiones activas ordenadas por fecha.
            entity.HasIndex(e => new { e.Status, e.IsPublic, e.CreatedAt });

            // Índice por región para facilitar estrategias de sharding o particionado horizontal
            entity.HasIndex(e => e.Region);
        });

        // Player
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionId).IsRequired().HasMaxLength(100);

            entity.OwnsOne(e => e.Position, position =>
            {
                position.Property(p => p.X).HasColumnName("PositionX");
                position.Property(p => p.Y).HasColumnName("PositionY");
            });

            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Relación UNICA con GameSession => evita GameSessionId1
            entity.HasOne(p => p.GameSession)
                  .WithMany(gs => gs.Players)
                  .HasForeignKey(p => p.GameSessionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.GameSessionId);
        });

        // Score
        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameDuration).HasConversion(v => v.Ticks, v => new TimeSpan(v));

            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.GameSession)
                  .WithMany(gs => gs.Scores)
                  .HasForeignKey(s => s.GameSessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GameSessionId);
            entity.HasIndex(e => e.AchievedAt);
            entity.HasIndex(e => e.Points);

            // Índice compuesto para ordenar por puntos y fecha al recuperar rankings.
            entity.HasIndex(e => new { e.Points, e.AchievedAt });
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasConversion<string>();

            entity.HasIndex(e => e.RoomId);
            entity.HasIndex(e => e.SentAt);

            // Índice compuesto para optimizar recuperación de mensajes por sala ordenados cronológicamente
            entity.HasIndex(e => new { e.RoomId, e.SentAt });
        });

        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<GameSession>().ToTable("game_sessions");
        modelBuilder.Entity<Player>().ToTable("players");
        modelBuilder.Entity<Score>().ToTable("scores");
        modelBuilder.Entity<ChatMessage>().ToTable("chat_messages");
    }
}
