using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Auth;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
