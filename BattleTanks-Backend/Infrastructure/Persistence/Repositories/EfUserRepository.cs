using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Infrastructure.Persistence.Repositories;

public class EfUserRepository : IUserRepository
{
    private readonly BattleTanksDbContext _context;

    public EfUserRepository(BattleTanksDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Scores)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail)
    {
        var normalized = usernameOrEmail.ToLowerInvariant();
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == normalized);
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string username, string email)
    {
        var normalized = email.ToLowerInvariant();
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username || u.Email == normalized);
    }

    public async Task<List<User>> GetTopPlayersByScoreAsync(int limit = 10)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.TotalScore)
            .ThenByDescending(u => u.GamesWon)
            .Take(limit)
            .ToListAsync();
    }

    public async Task UpdateLastLoginAsync(Guid id)
    {
        await _context.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastLoginAt, DateTime.UtcNow));
    }

    public async Task AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByIdAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}