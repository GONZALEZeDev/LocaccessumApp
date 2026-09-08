using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Locaccessum.Api.Common;

/// <summary>
/// Builds consistent <c>application/problem+json</c> error responses carrying a machine-readable
/// <c>code</c> extension member alongside the standard <see cref="ProblemDetails"/> fields.
/// </summary>
public static class ApiProblem
{
    public static ObjectResult Conflict(string code, string detail) => Problem(StatusCodes.Status409Conflict, code, detail);

    public static ObjectResult NotFound(string code, string detail) => Problem(StatusCodes.Status404NotFound, code, detail);

    public static ObjectResult Forbidden(string code, string detail) => Problem(StatusCodes.Status403Forbidden, code, detail);

    public static ObjectResult Validation(string code, string detail) => Problem(StatusCodes.Status400BadRequest, code, detail);

    public static ObjectResult Unauthorized(string code, string detail) => Problem(StatusCodes.Status401Unauthorized, code, detail);

    private static ObjectResult Problem(int status, string code, string detail)
    {
        var pd = new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status), Detail = detail };
        pd.Extensions["code"] = code;
        return new ObjectResult(pd) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
