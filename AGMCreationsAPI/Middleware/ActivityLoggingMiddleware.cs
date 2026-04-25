using Microsoft.AspNetCore.Mvc.Controllers;
using System.Text;

namespace SAPLZylemAPI
{
    public class ActivityLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        //private readonly IActivityLogger _logger;

        public ActivityLoggingMiddleware(RequestDelegate next/*, IActivityLogger logger*/)
        {
            _next = next;
            //_logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value;
            if (path.StartsWith("/swagger") || path.Contains("swagger", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var originalBodyStream = context.Response.Body;

            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            context.Request.EnableBuffering();
            string requestBody = await ReadRequestBodyAsync(context.Request);

            try
            {
                await _next(context); // Call next middleware / controller

                var menuKey = context.Items["MenuKey"] as string ?? "";
                // Rewind and read the response
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                string responseBody = "";// await new StreamReader(context.Response.Body).ReadToEndAsync();
                var rawResponse = await new StreamReader(context.Response.Body).ReadToEndAsync();

                // Try to parse if it's JSON stringified content
                if (IsJson(rawResponse))
                {
                    responseBody = rawResponse;
                }
                else if (rawResponse.StartsWith("\"") && rawResponse.EndsWith("\""))
                {
                    // It's a JSON-escaped string like: "\"[{\\\"Id\\\":1,...}]\""
                    // Remove outer quotes and unescape
                    responseBody = System.Text.Json.JsonSerializer.Deserialize<string>(rawResponse);
                }

                // Rewind again and copy to original stream
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalBodyStream);

                // Log after response is written
                var endpoint = context.GetEndpoint();
                var userId = context.User?.Identity?.Name ?? "Anonymous";
                var ip = context.Connection.RemoteIpAddress?.ToString();

                //var entry = new ActivityLogEntry
                //{
                //    UserId = userId,
                //    ProcessId = userId + "_" + DateTime.UtcNow.ToString("ddMMMyyyyhhmmss"),
                //    Action = endpoint?.DisplayName ?? "Unknown",
                //    MenuKey = menuKey ?? "",
                //    Controller = endpoint?.Metadata?.OfType<ControllerActionDescriptor>()
                //?.FirstOrDefault()?.ControllerName ?? "Unknown",
                //    Endpoint = context.Request.Path,
                //    HttpMethod = context.Request.Method,
                //    Timestamp = DateTime.UtcNow,
                //    IpAddress = ip,
                //    RequestBody = Truncate(requestBody, 5000),
                //    ResponseBody = Truncate(responseBody, 5000)
                //};

                //await _logger.LogAsync(entry);
            }
            catch (Exception ex)
            {
                // Optional: log the error here
                // throw;
            }
            finally
            {
                // Ensure the body stream is restored
                context.Response.Body = originalBodyStream;
            }
        }
        private bool IsJson(string input)
        {
            input = input?.Trim();
            return input?.StartsWith("{") == true || input?.StartsWith("[") == true;
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            string body = "";
            try
            {
                request.Body.Position = 0;

                using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

                body = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Reset so the next component can read it

                return Truncate(body, 5000); // Avoid excessively large logs
            }
            catch (Exception)
            {

            }
            return body;
        }

        private async Task<string> ReadResponseBodyAsync(HttpResponse response)
        {
            string body = "";
            try
            {
                response.Body.Seek(0, SeekOrigin.Begin);

                using var reader = new StreamReader(response.Body);
                body = await reader.ReadToEndAsync();

                response.Body.Seek(0, SeekOrigin.Begin); // Reset again before sending

                return Truncate(body, 5000);
            }
            catch (Exception ex)
            {

            }
            return body;
        }
        private string Truncate(string input, int maxLength)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return input;
                return input.Length <= maxLength ? input : input.Substring(0, maxLength) + "...[truncated]";
            }
            catch (Exception ex)
            {

            }
            return input;
        }
    }

}