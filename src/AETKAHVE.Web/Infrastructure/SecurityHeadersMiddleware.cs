namespace AETKAHVE.Web.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly PathString[] SensitiveEmailConfirmationPaths =
    [
        new("/account/profile/email-change/confirm"),
        new("/admin/invitation"),
        new("/admin/password-reset"),
        new("/admin/email-change/confirm"),
        new("/admin/security/email-change/confirm"),
        new("/superadmin/security/email-change/confirm")
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = IsSensitiveEmailConfirmationPath(context.Request.Path)
                ? "no-referrer"
                : "strict-origin-when-cross-origin";
            headers["Content-Security-Policy"] = "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

            if (context.Request.Path.StartsWithSegments("/admin") ||
                context.Request.Path.StartsWithSegments("/superadmin"))
            {
                headers.CacheControl = "no-store, no-cache, max-age=0";
                headers.Pragma = "no-cache";
                headers.Expires = "0";
            }

            return Task.CompletedTask;
        });
        await next(context);
    }

    private static bool IsSensitiveEmailConfirmationPath(PathString requestPath) =>
        SensitiveEmailConfirmationPaths.Any(path => requestPath.Equals(path));
}
