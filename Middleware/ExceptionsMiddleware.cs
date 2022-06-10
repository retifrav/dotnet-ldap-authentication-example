using Newtonsoft.Json;
using decovar.dev.Code;

namespace decovar.dev.Middleware;

public class ExceptionsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ExceptionsMiddleware(
        RequestDelegate next,
        IConfiguration configuration
        )
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            try
            {
                // List<string> headersList = new List<string>();
                // foreach (var header in httpContext.Request.Headers)
                // {
                //     headersList.Add($"{header.Key}:{header.Value}");
                // }
                // _logger.LogWarning($"Headers: {string.Join(",", headersList.ToArray())}");

                General.LogException(
                    ex,
                    _configuration
                );
            }
            catch //(Exception exLog)
            {
                // log it in some other way
            }

            // if (httpContext.Request.Path.ToString().StartsWith("/api/"))
            // {
            //     httpContext.Response.StatusCode = 500;
            //     httpContext.Response.ContentType = "application/json";
            //     // perhaps we shouldn't return exception detals to users
            //     var errorJson = new
            //     {
            //         error = "Internal server error"
            //     };
            //     await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(errorJson));
            // }
            // else
            {
                throw;
            }
        }
    }
}

public static class ExceptionsMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomExceptionsHandler(
        this IApplicationBuilder builder
        )
    {
        return builder.UseMiddleware<ExceptionsMiddleware>();
    }
}
