using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Application.DTOs;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;
    private readonly IDistributedCache _cache;

    public AuthController(
        IAuthService authService,
        IJwtService jwtService,
        ILogger<AuthController> logger,
        IDistributedCache cache)
    {
        _authService = authService;
        _jwtService = jwtService;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>Registrar un nuevo jugador</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            var user = await _authService.RegisterAsync(registerDto);
            var token = _jwtService.GenerateAccessToken(user);
            SetJwtCookie(token); // cookie HttpOnly, Lax, Secure=false (HTTP local)
            await _cache.SetStringAsync($"session:{token}", user.Id.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

            _logger.LogInformation("User {Username} registered successfully", registerDto.Username);

            return Ok(new
            {
                success = true,
                message = "Registration successful",
                user = new UserDto(
                    user.Id.ToString(),
                    user.Username,
                    user.Email,
                    user.GamesPlayed,
                    user.GamesWon,
                    user.TotalScore,
                    user.WinRate,
                    user.CreatedAt
                )
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Registration failed for {Username}: {Error}", registerDto.Username, ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for {Username}", registerDto.Username);
            return StatusCode(500, new { success = false, message = "An error occurred during registration" });
        }
    }

    /// <summary>Iniciar sesión</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            var user = await _authService.LoginAsync(loginDto);
            var token = _jwtService.GenerateAccessToken(user);
            SetJwtCookie(token);
            await _cache.SetStringAsync($"session:{token}", user.Id.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            return Ok(new
            {
                success = true,
                message = "Login successful",
                user = new UserDto(
                    user.Id.ToString(),
                    user.Username,
                    user.Email,
                    user.GamesPlayed,
                    user.GamesWon,
                    user.TotalScore,
                    user.WinRate,
                    user.CreatedAt
                )
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for {UsernameOrEmail}: {Error}", loginDto.UsernameOrEmail, ex.Message);
            return Unauthorized(new { success = false, message = "Invalid credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {UsernameOrEmail}", loginDto.UsernameOrEmail);
            return StatusCode(500, new { success = false, message = "An error occurred during login" });
        }
    }

    /// <summary>Obtener perfil del usuario autenticado</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, message = "Invalid user token" });
            }

            var user = await _authService.GetUserProfileAsync(userId);

            return Ok(new { success = true, user });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(500, new { success = false, message = "An error occurred while retrieving profile" });
        }
    }

    /// <summary>Cerrar sesión</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        try
        {
            // Borrar cookie en HTTP local
            Response.Cookies.Delete("jwt", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = false,
                Path = "/"
            });

            if (Request.Cookies.TryGetValue("jwt", out var token))
            {
                _cache.Remove($"session:{token}");
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            _logger.LogInformation("User {Username} logged out successfully", username);

            return Ok(new { success = true, message = "Logout successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { success = false, message = "An error occurred during logout" });
        }
    }

    /// <summary>Verificar si el usuario está autenticado</summary>
    [HttpGet("verify")]
    public IActionResult VerifyToken()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Ok(new
        {
            success = true,
            authenticated = true,
            userId,
            username
        });
    }

    private void SetJwtCookie(string token)
    {
        // HTTP local: Lax + Secure=false
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Expires = DateTime.UtcNow.AddHours(1),
            Path = "/"
        };

        Response.Cookies.Append("jwt", token, cookieOptions);
    }
}
