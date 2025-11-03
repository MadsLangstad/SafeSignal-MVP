using System.Security.Claims;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Middleware;

/// <summary>
/// Middleware for automatically logging HTTP requests to audit log
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private static readonly HashSet<string> _auditedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/logout",
        "/api/auth/refresh",
        "/api/auth/change-password",
        "/api/organizations",
        "/api/users",
        "/api/devices",
        "/api/alerts",
        "/api/buildings",
        "/api/rooms"
    };

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        // Capture original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            // Only audit specific paths and methods
            if (ShouldAudit(context))
            {
                // Use memory stream to capture response
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                var startTime = DateTime.UtcNow;

                try
                {
                    await _next(context);
                }
                finally
                {
                    // Log the request after completion
                    await LogRequestAsync(context, auditService, startTime);

                    // Copy response back to original stream
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            else
            {
                // Skip audit logging for non-audited paths
                await _next(context);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private bool ShouldAudit(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Audit all POST, PUT, PATCH, DELETE requests to API
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var method = context.Request.Method;
            if (method == "POST" || method == "PUT" || method == "PATCH" || method == "DELETE")
            {
                return true;
            }
        }

        // Also audit specific GET requests (like sensitive data access)
        return _auditedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LogRequestAsync(HttpContext context, IAuditService auditService, DateTime startTime)
    {
        try
        {
            var user = context.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value;
            // JWT claims are case-sensitive - use lowercase to match JwtTokenService emission
            var organizationId = user.FindFirst("organizationId")?.Value;

            var auditLog = new AuditLog
            {
                UserId = !string.IsNullOrEmpty(userId) ? Guid.Parse(userId) : null,
                UserEmail = userEmail,
                OrganizationId = !string.IsNullOrEmpty(organizationId) ? Guid.Parse(organizationId) : null,
                Action = GetActionFromRequest(context),
                EntityType = GetEntityTypeFromPath(context.Request.Path),
                Category = GetCategoryFromPath(context.Request.Path),
                HttpMethod = context.Request.Method,
                RequestPath = context.Request.Path.Value,
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                StatusCode = context.Response.StatusCode,
                Success = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300,
                Timestamp = startTime
            };

            await auditService.LogAsync(auditLog);
        }
        catch (Exception ex)
        {
            // Never fail the request due to audit logging failure
            _logger.LogError(ex, "Failed to log audit entry for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
    }

    private string GetActionFromRequest(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        // Extract action from path and method
        return method switch
        {
            "POST" when path.Contains("/login") => "Login",
            "POST" when path.Contains("/logout") => "Logout",
            "POST" when path.Contains("/refresh") => "RefreshToken",
            "POST" when path.Contains("/change-password") => "ChangePassword",
            "POST" => $"Create{GetEntityTypeFromPath(context.Request.Path)}",
            "PUT" => $"Update{GetEntityTypeFromPath(context.Request.Path)}",
            "PATCH" => $"Patch{GetEntityTypeFromPath(context.Request.Path)}",
            "DELETE" => $"Delete{GetEntityTypeFromPath(context.Request.Path)}",
            "GET" => $"Read{GetEntityTypeFromPath(context.Request.Path)}",
            _ => $"{method}_{path}"
        };
    }

    private string GetEntityTypeFromPath(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;
        var segments = pathValue.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Extract entity type from path (e.g., /api/organizations -> Organization)
        if (segments.Length >= 2 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var entitySegment = segments[1];

            // Convert plural to singular
            if (entitySegment.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            {
                return char.ToUpper(entitySegment[0]) + entitySegment[1..^3] + "y";
            }
            if (entitySegment.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return char.ToUpper(entitySegment[0]) + entitySegment[1..^1];
            }

            return char.ToUpper(entitySegment[0]) + entitySegment[1..];
        }

        return "Unknown";
    }

    private AuditCategory GetCategoryFromPath(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;

        if (pathValue.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.Authentication;
        }
        if (pathValue.Contains("/alerts/", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.Alert;
        }
        if (pathValue.Contains("/devices/", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.Device;
        }

        return AuditCategory.DataModification;
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
