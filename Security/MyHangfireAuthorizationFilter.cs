using Hangfire.Dashboard;

namespace SmartClinic.Security
{
    public class MyHangfireAuthorizationFilter: IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            if (httpContext.User.Identity == null || !httpContext.User.Identity.IsAuthenticated)
            {
                return false;
            }

            var roleMaskClaim = httpContext.User.FindFirst("RoleMask")?.Value;

            if (int.TryParse(roleMaskClaim, out int roleMask))
            {
                return (roleMask & 16) == 16;
            }

            // Nếu không có RoleMask hoặc không phải Admin -> Chặn!
            return false;
        }
    }
}
