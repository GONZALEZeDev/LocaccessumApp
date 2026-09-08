using Locaccessum.Domain.Enums;

namespace Locaccessum.Domain.Entities;

public class Equipment
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public string? Informations { get; set; }
    public EquipmentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
