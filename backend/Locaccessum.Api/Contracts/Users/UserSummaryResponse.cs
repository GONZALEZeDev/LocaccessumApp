namespace Locaccessum.Api.Contracts.Users;

public record UserSummaryResponse(Guid UserId, string DisplayName, string UserCode);
