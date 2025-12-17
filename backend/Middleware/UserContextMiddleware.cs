using System.Security.Claims;

namespace backend.Middleware
{
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? context.User.FindFirst(ClaimTypes.Name)?.Value
                             ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var tenantId = context.User.FindFirst("tenantId")?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
                var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    context.Items["UserId"] = userId;
                }
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    context.Items["TenantId"] = tenantId;
                }
                if (!string.IsNullOrWhiteSpace(email))
                {
                    context.Items["Email"] = email;
                }
                if (!string.IsNullOrWhiteSpace(role))
                {
                    context.Items["Role"] = role;
                }
            }

            await _next(context);
        }
    }
}

