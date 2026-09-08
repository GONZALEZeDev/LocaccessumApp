using Locaccessum.Domain.Enums;

namespace Locaccessum.Domain.Entities;

public class Membership
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public MembershipRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
