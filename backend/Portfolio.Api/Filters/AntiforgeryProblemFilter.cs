using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Portfolio.Api.Filters
{
    public class AntiforgeryProblemFilter : IAlwaysRunResultFilter, IOrderedFilter
    {
        public int Order => -3000;

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is AntiforgeryValidationFailedResult)
            {
                context.Result = new ObjectResult(new ProblemDetails()
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Antiforgery validation failed.",
                    Detail = "Refresh the CSRF token and submit the request again."
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }
}
