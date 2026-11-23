namespace BabAlHara.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, string DisplayName);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
