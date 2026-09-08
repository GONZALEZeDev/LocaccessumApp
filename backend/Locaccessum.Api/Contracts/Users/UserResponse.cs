namespace Locaccessum.Api.Contracts.Users;

public record UserResponse(Guid UserId, string Email, string DisplayName, string UserCode, DateTimeOffset CreatedAt);
