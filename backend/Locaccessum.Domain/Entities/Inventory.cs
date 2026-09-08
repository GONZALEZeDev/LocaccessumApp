namespace Locaccessum.Domain.Entities;

public class Inventory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
