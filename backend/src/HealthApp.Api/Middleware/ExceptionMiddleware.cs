using System.Text.Json;
namespace HealthApp.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next,ILogger<ExceptionMiddleware> logger) {
    public async Task InvokeAsync(HttpContext context) {
        try {
            await next(context);
        }
        catch(KeyNotFoundException ex) {
            await Write(context,404,ex.Message);
        }
        catch(UnauthorizedAccessException ex) {
            await Write(context,401,ex.Message);
        }
        catch(ArgumentException ex) {
            await Write(context,400,ex.Message);
        }
        catch(InvalidOperationException ex) {
            await Write(context,409,ex.Message);
        }
        catch(Exception ex) {
            logger.LogError(ex,"Unhandled exception");
            await Write(context,500,"An unexpected error occurred.");
        }
    }
    static async Task Write(HttpContext c,int status,string message) {
        c.Response.StatusCode=status;
        c.Response.ContentType="application/json";
        await c.Response.WriteAsync(JsonSerializer.Serialize(new {
            status,message
        }));
    }
}
