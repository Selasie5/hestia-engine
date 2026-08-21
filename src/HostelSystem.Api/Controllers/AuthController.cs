using HostelSystem.Api.Controllers.Auth;
using HostelSystem.Domain.Entities;
using HostelSystem.Identity.Models;
using HostelSystem.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
// using HostelSystem.Application.Common.Interfaces; // <-- adjust: wherever IApplicationDbContext lives

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v1.0/Auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppIdentityDbContext _identityContext;
    // private readonly IApplicationDbContext _appContext; // <-- CHECK: swap for your actual Student persistence mechanism
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        AppIdentityDbContext identityContext,
        // IApplicationDbContext appContext,
        Microsoft.Extensions.Options.IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _identityContext = identityContext;
        // _appContext = appContext;
        _jwtSettings = jwtSettings.Value;
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

        // CHECK: replace with your actual Gender enum parsing + Student persistence
        var gender = Enum.Parse<HostelSystem.Domain.Enums.Gender>(request.Gender, ignoreCase: true);
        var student = new Student(user.Id, request.StudentNumber, request.FirstName, request.LastName, gender);

        // TODO: persist Student via [whoever's] repository/DbContext — see issue for DB owner
        // _appContext.Students.Add(student);
        // await _appContext.SaveChangesAsync();

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