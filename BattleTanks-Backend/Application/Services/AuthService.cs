using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using BCrypt.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;

    public AuthService(IUserRepository userRepository, IMemoryCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    // Register new user
    public async Task<User> RegisterAsync(RegisterDto registerDto)
    {
        if (registerDto.Password != registerDto.ConfirmPassword)
            throw new ArgumentException("Passwords don't match");

        var usernameExistsTask = _userRepository.ExistsByUsernameAsync(registerDto.Username);
        var emailExistsTask = _userRepository.ExistsByEmailAsync(registerDto.Email);
        await Task.WhenAll(usernameExistsTask, emailExistsTask);

        if (usernameExistsTask.Result)
            throw new ArgumentException("Username already exists");

        if (emailExistsTask.Result)
            throw new ArgumentException("Email already exists");

        var passwordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(registerDto.Password));
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

        var validPassword = await Task.Run(() => BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash));
        if (!validPassword)
            throw new UnauthorizedAccessException("Invalid credentials");

        user.UpdateLastLogin();
        await _userRepository.UpdateLastLoginAsync(user.Id);
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
