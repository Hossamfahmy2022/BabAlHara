using System.Security.Claims;
using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using BabAlHara.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    JwtTokenService jwtService) : ControllerBase
{
    private readonly PasswordHasher<AdminUser> _hasher = new();

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "بيانات الدخول غير صحيحة",
                Detail = "اسم المستخدم أو كلمة المرور غير صحيحة.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "بيانات الدخول غير صحيحة",
                Detail = "اسم المستخدم أو كلمة المرور غير صحيحة.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiresAt) = jwtService.CreateToken(user);
        return Ok(new LoginResponse(token, expiresAt, user.DisplayName));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return Unauthorized();
        }

        var currentCheck = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? string.Empty);
        if (currentCheck == PasswordVerificationResult.Failed)
        {
            throw new DomainValidationException("currentPassword", "كلمة المرور الحالية غير صحيحة.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new DomainValidationException("newPassword", "كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل.");
        }

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح." });
    }
}
