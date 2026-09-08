namespace Locaccessum.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string UserCode { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
