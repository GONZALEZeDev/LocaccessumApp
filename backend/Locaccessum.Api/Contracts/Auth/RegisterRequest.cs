using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, StringLength(100, MinimumLength = 1)] string DisplayName);
