namespace Locaccessum.Infrastructure.Identity;

public interface IUserCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
