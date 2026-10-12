using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
namespace BackEnd
{
    public class AllowAllCorsMiddleware
    {
        private readonly RequestDelegate _next;

        public AllowAllCorsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "*";
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

            // Short-circuit the OPTIONS request
            if (context.Request.Method == HttpMethods.Options)
            {
                context.Response.StatusCode = 204;
                return;
            }

            await _next(context);
        }
    }
}

