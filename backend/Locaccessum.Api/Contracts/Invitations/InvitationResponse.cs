namespace Locaccessum.Api.Contracts.Invitations;

public record InvitationResponse(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    Guid InvitedUserId,
    Guid InvitedByUserId,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);
