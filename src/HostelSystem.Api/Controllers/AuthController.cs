using HostelSystem.Api.Controllers.Auth;
using HostelSystem.Domain.Entities;
using HostelSystem.Identity.Models;
using HostelSystem.Identity.Services;
using HostelSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v1.0/Auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppIdentityDbContext _identityContext;
    private readonly AppDbContext _appContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        AppIdentityDbContext identityContext,
        AppDbContext appContext,
        Microsoft.Extensions.Options.IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _identityContext = identityContext;
        _appContext = appContext;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Conflict("A user with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRoleAsync(user, "Student");

        // Persist Student domain entity (issue #11)
        if (!Enum.TryParse<HostelSystem.Domain.Enums.Gender>(request.Gender, ignoreCase: true, out var gender))
            gender = HostelSystem.Domain.Enums.Gender.Male;

        var student = new Student(user.Id, request.StudentNumber, request.FirstName, request.LastName, gender);

        try
        {
            _appContext.Students.Add(student);
            await _appContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.InnerException?.Message.Contains("unique") == true)
        {
            _logger.LogWarning(ex, "Student persistence failed for {Email} — rolling back user", request.Email);
            await _userManager.DeleteAsync(user);
            return BadRequest(new { error = "Student number already exists." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Student for {Email}", request.Email);
            await _userManager.DeleteAsync(user);
            return BadRequest(new { error = "Failed to create student profile: " + ex.Message });
        }

        return await IssueTokensAsync(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        return await IssueTokensAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var stored = await _identityContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored == null || !stored.IsActive)
            return Unauthorized("Invalid or expired refresh token.");

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user == null)
            return Unauthorized();

        // Rotate: revoke the old one, issue a new one
        stored.RevokedAt = DateTime.UtcNow;

        return await IssueTokensAsync(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        var stored = await _identityContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored != null && stored.IsActive)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _identityContext.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(ApplicationUser user)
    {
        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user);

        _identityContext.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
        });
        await _identityContext.SaveChangesAsync();

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
        ));
    }
}