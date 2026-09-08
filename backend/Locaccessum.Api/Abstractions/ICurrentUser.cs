namespace Locaccessum.Api.Abstractions;

public interface ICurrentUser
{
    Guid Id { get; }

    string Email { get; }
}
