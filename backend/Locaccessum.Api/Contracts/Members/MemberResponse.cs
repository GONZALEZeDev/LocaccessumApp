namespace Locaccessum.Api.Contracts.Members;

public record MemberResponse(Guid UserId, string DisplayName, string UserCode, string Role, DateTimeOffset JoinedAt);
