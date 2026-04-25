using System.Security.Claims;

namespace SAPLZylemAPI
{

    public class UsageLogMiddleware
    {
        private readonly RequestDelegate _next;

        public UsageLogMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context/*, UsageLogDB usageLogService*/)
        {
            try
            {
                var path = context.Request.Path.Value;
                Console.WriteLine($"UsageLogMiddleware invoked: {context.Request.Path}");
                // Important! Let the request proceed
                await _next(context);
                // Skip logging for health, swagger, auth
                if (!path.StartsWith("/swagger") && !path.Contains("/health") && !path.Contains("/Login"))
                {
                    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
                    var sessionIdClaim = context.User.FindFirst("sessionId")?.Value;
                    var sessionId = Guid.TryParse(sessionIdClaim, out var sid) ? sid : Guid.Empty;

                    var ip = context.Connection.RemoteIpAddress?.ToString();
                    var userAgent = context.Request.Headers["User-Agent"].ToString();

                    var additionalData = context.Items["UsageLogAdditionalData"] as string ?? "";

                    //var log = new UsageLogDTO
                    //{
                    //    UserId = userId,
                    //    SessionId = sessionId,
                    //    Action = path,
                    //    Timestamp = DateTime.UtcNow,
                    //    IPAddress = ip,
                    //    UserAgent = userAgent,
                    //    AdditionalData = additionalData
                    //};

                    //// Log asynchronously
                    //Task.Run(() => usageLogService.Log(log));
                }
            }
            catch (Exception ex)
            {
                // Don't crash pipeline if logging fails
                Console.WriteLine("UsageLogMiddleware error: " + ex.Message);
            }

        }
    }

}