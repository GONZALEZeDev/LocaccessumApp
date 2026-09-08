using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Invitations;

public record CreateInvitationRequest([Required] string UserCode, [Required] string Role);
