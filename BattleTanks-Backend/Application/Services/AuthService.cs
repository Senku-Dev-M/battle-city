using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using BCrypt.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _env;

    public AuthService(IUserRepository userRepository, IMemoryCache cache, IHostEnvironment env)
    {
        _userRepository = userRepository;
        _cache = cache;
        _env = env;
    }

    // Register new user
    public async Task<User> RegisterAsync(RegisterDto registerDto)
    {
        if (registerDto.Password != registerDto.ConfirmPassword)
            throw new ArgumentException("Passwords don't match");

        var existing = await _userRepository.GetByUsernameOrEmailAsync(registerDto.Username, registerDto.Email);
        if (existing != null)
        {
            if (existing.Username == registerDto.Username)
                throw new ArgumentException("Username already exists");
            if (existing.Email == registerDto.Email.ToLowerInvariant())
                throw new ArgumentException("Email already exists");
        }
        var workFactor = _env.IsProduction() ? 11 : 10;
        var passwordHash = await Task.Run(() => BCrypt.Net.BCrypt.EnhancedHashPassword(registerDto.Password, workFactor: workFactor));
        var user = User.Create(registerDto.Username, registerDto.Email, passwordHash);

        await _userRepository.AddAsync(user);
        _cache.Set($"user:{user.Username.ToLowerInvariant()}", user, TimeSpan.FromMinutes(5));
        _cache.Set($"user:{user.Email}", user, TimeSpan.FromMinutes(5));
        return user;
    }

    // Login user
    public async Task<User> LoginAsync(LoginDto loginDto)
    {
        var cacheKey = $"user:{loginDto.UsernameOrEmail.ToLowerInvariant()}";
        if (!_cache.TryGetValue(cacheKey, out User? user))
        {
            user = await _userRepository.GetByUsernameOrEmailAsync(loginDto.UsernameOrEmail);
            if (user != null)
            {
                _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
            }
        }

        if (user == null)
            throw new UnauthorizedAccessException("Invalid credentials");

        var validPassword = await Task.Run(() => BCrypt.Net.BCrypt.EnhancedVerify(loginDto.Password, user.PasswordHash));
        if (!validPassword)
            throw new UnauthorizedAccessException("Invalid credentials");

        var now = DateTime.UtcNow;
        if (now - user.LastLoginAt >= TimeSpan.FromMinutes(5))
        {
            user.UpdateLastLogin();
            await _userRepository.UpdateLastLoginAsync(user.Id);
        }
        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));

        return user;
    }

    // Get profile by user id
    public async Task<UserDto> GetUserProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        return MapToUserDto(user);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            user.Id.ToString(),
            user.Username,
            user.Email,
            user.GamesPlayed,
            user.GamesWon,
            user.TotalScore,
            user.WinRate,
            user.CreatedAt
        );
    }
}
