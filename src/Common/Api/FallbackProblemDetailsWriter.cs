using Microsoft.AspNetCore.WebUtilities;

namespace server.Common.Api;

/// <summary>
/// Writes ProblemDetails for clients whose Accept header excludes JSON, e.g. "text/html".
/// The built-in writer refuses those, and the error would be sent in another format.
/// </summary>
public class FallbackProblemDetailsWriter : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var response = context.HttpContext.Response;
        var problem = context.ProblemDetails;
        problem.Status ??= response.StatusCode;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(problem.Status.Value);

        // Use the runtime type, so that validation errors are included.
        await response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json");
    }
}
