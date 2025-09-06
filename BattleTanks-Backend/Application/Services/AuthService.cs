using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using BCrypt.Net;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    // Register new user
    public async Task<User> RegisterAsync(RegisterDto registerDto)
    {
        if (registerDto.Password != registerDto.ConfirmPassword)
            throw new ArgumentException("Passwords don't match");

        if (await _userRepository.ExistsByUsernameAsync(registerDto.Username))
            throw new ArgumentException("Username already exists");

        if (await _userRepository.ExistsByEmailAsync(registerDto.Email))
            throw new ArgumentException("Email already exists");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);
        var user = User.Create(registerDto.Username, registerDto.Email, passwordHash);
        
        await _userRepository.AddAsync(user);
        return user;
    }

    // Login user
    public async Task<User> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByUsernameAsync(loginDto.UsernameOrEmail) ??
                   await _userRepository.GetByEmailAsync(loginDto.UsernameOrEmail);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

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
