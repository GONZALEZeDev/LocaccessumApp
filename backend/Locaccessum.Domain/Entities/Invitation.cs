using Locaccessum.Domain.Enums;

namespace Locaccessum.Domain.Entities;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public Guid InvitedUserId { get; set; }
    public User InvitedUser { get; set; } = null!;
    public Guid InvitedByUserId { get; set; }
    public User InvitedBy { get; set; } = null!;
    public MembershipRole Role { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
