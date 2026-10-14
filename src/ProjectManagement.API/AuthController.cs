using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ProjectManagement.Infrastructure.Persistence;

namespace ProjectManagement.API.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IConfiguration config) : ControllerBase
{
    public sealed record RegisterRequest(string Email, string Password, string DisplayName);
    public sealed record LoginRequest(string Email, string Password);
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.DisplayName) || r.DisplayName.Length > 120) return BadRequest(new { message = "Valid email and display name are required." });
        var user = new ApplicationUser { UserName = r.Email.Trim(), Email = r.Email.Trim(), DisplayName = r.DisplayName.Trim() };
        var result = await users.CreateAsync(user, r.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        await users.AddToRoleAsync(user, "Developer"); return Ok(new { userId = user.Id, message = "Account created. Sign in to obtain a token." });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var user = await users.FindByEmailAsync(r.Email);
        if (user is null || !await users.CheckPasswordAsync(user, r.Password)) return Unauthorized(new { message = "Invalid credentials." });
        if (await users.IsLockedOutAsync(user)) return Unauthorized(new { message = "Account temporarily locked." });
        var roles = await users.GetRolesAsync(user); var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub,user.Id), new(ClaimTypes.NameIdentifier,user.Id), new(JwtRegisteredClaimNames.Email,user.Email ?? "") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var jwt = config.GetSection("Jwt"); var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)); var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims, expires: DateTime.UtcNow.AddMinutes(int.TryParse(jwt["ExpiresMinutes"], out var mins) ? mins : 60), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc = token.ValidTo, userId = user.Id, roles });
    }
}
