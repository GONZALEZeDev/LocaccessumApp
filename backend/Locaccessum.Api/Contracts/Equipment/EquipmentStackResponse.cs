namespace Locaccessum.Api.Contracts.Equipment;

public record EquipmentStackResponse(string Name, string Reference, int UnitsTotal, int UnitsActive, int UnitsMaintenance, int UnitsRetired, Guid[] UnitIds);
