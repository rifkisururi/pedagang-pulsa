using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace PedagangPulsa.Application.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LogActivityAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var loggerFactory = (ILoggerFactory?)context.HttpContext.RequestServices.GetService(typeof(ILoggerFactory));
        var logger = loggerFactory?.CreateLogger("PedagangPulsa.Application.Attributes.LogActivityAttribute");

        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();

        logger?.LogInformation("Executing Action: {ControllerName}.{ActionName}", controllerName, actionName);

        var executedContext = await next();

        if (executedContext.Exception == null)
        {
            logger?.LogInformation("Executed Action Successfully: {ControllerName}.{ActionName}", controllerName, actionName);
        }
        else
        {
            logger?.LogError(executedContext.Exception, "Executed Action with Error: {ControllerName}.{ActionName}", controllerName, actionName);
        }
    }
}
