using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EncryptedChat.Filters;

public class ValidateModelFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new { Message = "Invalid request" });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
