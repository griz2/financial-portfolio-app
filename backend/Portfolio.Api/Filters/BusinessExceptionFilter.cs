using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Domain;
using Portfolio.Core.Management;

namespace Portfolio.Api.Filters
{
    public class BusinessExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            int status;
            string message;

            if (context.Exception is ResourceNotFoundException)
            {
                status = 404;
                message = "The requested record was not found.";
            }
            else if (context.Exception is FinancialRuleException)
            {
                status = 400;
                message = context.Exception.Message;
            }
            else if (context.Exception is DbUpdateConcurrencyException)
            {
                status = 409;
                message = "The record changed during this operation. Refresh before trying again.";
            }
            else if (context.Exception is DbUpdateException update &&
                update.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                status = 409;
                message = "A record with these details already exists. Refresh and check your entries.";
            }
            else
            {
                return;
            }

            context.Result = new ObjectResult(new ProblemDetails()
            {
                Status = status,
                Title = message
            })
            { StatusCode = status };
            context.ExceptionHandled = true;
        }
    }
}
