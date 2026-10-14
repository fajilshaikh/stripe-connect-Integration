using API.Errors;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
{
    /// 404 with the same body shape as ExceptionMiddleware, so the client reads one error format.
    protected NotFoundObjectResult NotFoundError(string message) =>
        NotFound(new ApiErrorResponse(StatusCodes.Status404NotFound, message));
}
