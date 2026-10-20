using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    // Capped (not just Required) so a request can't force a full-length PBKDF2 verify over an
    // arbitrarily large payload — see RegisterRequest.Password for the matching register-side cap.
    [Required, StringLength(128, MinimumLength = 1)] string Password);
