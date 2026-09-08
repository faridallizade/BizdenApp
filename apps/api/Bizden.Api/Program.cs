using System.Security.Claims;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Bizden.Application.Authentication;
using Bizden.Application.Events;
using Bizden.Application.Invitations;
using Bizden.Application.PublicAccess;
using Bizden.Application.Photos;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.DependencyInjection;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.Observability;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddHealthChecks();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65_536);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = isDevelopment ? "bizden-auth" : "__Host-bizden-auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The API is reachable only from the Docker network; Caddy/Nginx addresses are dynamic.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = isDevelopment ? "bizden-csrf" : "__Host-bizden-csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("host-auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("public-qr", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var token = context.Request.RouteValues["token"]?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{ip}:{token}", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
    {
        if (context.Request.Path.StartsWithSegments("/api/host") && !context.Request.Path.StartsWithSegments("/api/host/antiforgery"))
        {
            try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { context.Response.StatusCode = StatusCodes.Status400BadRequest; await context.Response.WriteAsJsonAsync(new { code = "INVALID_CSRF", message = "Request could not be verified." }); return; }
        }
    }
    await next();
});
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew(); await next();
    context.RequestServices.GetRequiredService<RuntimeMetrics>().RecordRequest(context.Response.StatusCode);
    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Bizden.RequestAudit")
        .LogInformation("HTTP request completed {Method} {StatusCode} in {ElapsedMilliseconds}ms", context.Request.Method, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
});
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Bizdən API", status = "ready" }));
app.MapHealthChecks("/health");
app.MapGet("/health/ready", async (BizdenDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
var metricsApiKey = builder.Configuration["Monitoring:MetricsApiKey"];
app.MapGet("/metrics", (HttpContext context, RuntimeMetrics metrics) =>
{
    if (string.IsNullOrWhiteSpace(metricsApiKey) || !string.Equals(context.Request.Headers["X-Metrics-Key"], metricsApiKey, StringComparison.Ordinal))
        return Results.NotFound();
    return Results.Text(metrics.ToPrometheusText(), "text/plain; version=0.0.4");
});
app.MapGet("/api/host/antiforgery", (IAntiforgery antiforgery, HttpContext context) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));

var auth = app.MapGroup("/api/host/auth").RequireRateLimiting("host-auth");
auth.MapPost("/register", async (RegisterHostRequest request, IHostAuthenticationService service, HttpContext context, CancellationToken cancellationToken) =>
{
    var result = await service.RegisterAsync(new RegisterHostCommand(request.Name, request.Email, request.Password), cancellationToken);
    if (result.RequiresEmailVerification && result.ErrorCode is not null) return AuthError(result.ErrorCode, StatusCodes.Status503ServiceUnavailable);
    if (result.RequiresEmailVerification) return Results.Accepted($"/api/host/auth/verify-email", new { requiresEmailVerification = true });
    if (!result.Succeeded) return AuthError(result.ErrorCode!, StatusCodes.Status400BadRequest);
    await SignInAsync(context, result.User!);
    return Results.Created("/api/host/auth/me", new HostSessionResponse(result.User!.Id, result.User.Name, result.User.Email));
});
auth.MapPost("/verify-email", async (VerifyHostEmailRequest request, IHostAuthenticationService service, HttpContext context, CancellationToken cancellationToken) =>
{
    var result = await service.VerifyEmailAsync(new VerifyHostEmailCommand(request.Email, request.Code), cancellationToken);
    if (!result.Succeeded) return AuthError(result.ErrorCode!, StatusCodes.Status400BadRequest);
    await SignInAsync(context, result.User!);
    return Results.Ok(new HostSessionResponse(result.User!.Id, result.User.Name, result.User.Email));
});
auth.MapPost("/resend-verification", async (ResendVerificationRequest request, IHostAuthenticationService service, CancellationToken cancellationToken) =>
{
    var result = await service.ResendVerificationAsync(request.Email, cancellationToken);
    return result.ErrorCode is null ? Results.Accepted() : AuthError(result.ErrorCode, result.ErrorCode == "VERIFICATION_RATE_LIMITED" ? StatusCodes.Status429TooManyRequests : StatusCodes.Status503ServiceUnavailable);
});
auth.MapPost("/login", async (LoginHostRequest request, IHostAuthenticationService service, HttpContext context, CancellationToken cancellationToken) =>
{
    var result = await service.AuthenticateAsync(new LoginHostCommand(request.Email, request.Password), cancellationToken);
    if (!result.Succeeded) return AuthError("INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
    await SignInAsync(context, result.User!);
    return Results.Ok(new HostSessionResponse(result.User!.Id, result.User.Name, result.User.Email));
});
auth.MapPost("/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); }).RequireAuthorization();
auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new HostSessionResponse(
    Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), user.FindFirstValue(ClaimTypes.Name)!, user.FindFirstValue(ClaimTypes.Email)!))).RequireAuthorization();

var events = app.MapGroup("/api/host/events").RequireAuthorization();
events.MapGet("/", async (ClaimsPrincipal user, IHostEventService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(OwnerId(user), cancellationToken)));
events.MapPost("/", async (CreateEventRequest request, ClaimsPrincipal user, IHostEventService service, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.CreateAsync(OwnerId(user), new CreateHostEventCommand(request.Name, request.Description, request.EventDate, request.TimeZone, request.UploadStartAt, request.UploadEndAt, request.Status), cancellationToken);
        return Results.Created($"/api/host/events/{result.Id}", result);
    }
    catch (ArgumentException exception) { return ValidationError(exception.Message); }
});
events.MapGet("/{eventId:guid}", async (Guid eventId, ClaimsPrincipal user, IHostEventService service, CancellationToken cancellationToken) =>
    await service.GetAsync(OwnerId(user), eventId, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound());
events.MapPut("/{eventId:guid}", async (Guid eventId, UpdateEventRequest request, ClaimsPrincipal user, IHostEventService service, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.UpdateAsync(OwnerId(user), eventId, new UpdateHostEventCommand(request.Name, request.Description, request.EventDate, request.TimeZone, request.UploadStartAt, request.UploadEndAt, request.Status), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (ArgumentException exception) { return ValidationError(exception.Message); }
});

events.MapGet("/{eventId:guid}/invitations", async (Guid eventId, ClaimsPrincipal user, IInvitationManagementService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(OwnerId(user), eventId, cancellationToken)));
events.MapPost("/{eventId:guid}/invitations", async (Guid eventId, CreateInvitationRequest request, ClaimsPrincipal user, IInvitationManagementService service, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.CreateAsync(OwnerId(user), new CreateInvitationBatchCommand(eventId, request.Label, request.UploadLimit, request.ExpiresAt, request.Count), cancellationToken);
        return result is null ? Results.NotFound() : Results.Created($"/api/host/events/{eventId}/invitations", result);
    }
    catch (ArgumentException exception) { return ValidationError(exception.Message); }
});
events.MapPatch("/{eventId:guid}/invitations/{invitationId:guid}", async (Guid eventId, Guid invitationId, UpdateInvitationRequest request, ClaimsPrincipal user, IInvitationManagementService service, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.UpdateAsync(OwnerId(user), eventId, invitationId, new UpdateInvitationCommand(request.Label, request.UploadLimit, request.ExpiresAt, request.IsActive), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (ArgumentException exception) { return ValidationError(exception.Message); }
});
events.MapPost("/{eventId:guid}/invitations/{invitationId:guid}/regenerate", async (Guid eventId, Guid invitationId, ClaimsPrincipal user, IInvitationManagementService service, CancellationToken cancellationToken) =>
    await service.RegenerateAsync(OwnerId(user), eventId, invitationId, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound());
events.MapGet("/{eventId:guid}/photos", async (Guid eventId, Guid? invitationId, int? page, int? pageSize, ClaimsPrincipal user, IHostPhotoService service, CancellationToken cancellationToken) =>
    await service.ListAsync(OwnerId(user), eventId, invitationId, page ?? 1, pageSize ?? 24, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound());

var photos = app.MapGroup("/api/host/photos").RequireAuthorization();
photos.MapGet("/{photoId:guid}/download", async (Guid photoId, ClaimsPrincipal user, IHostPhotoService service, CancellationToken cancellationToken) =>
    await service.GetDownloadAsync(OwnerId(user), photoId, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound());
photos.MapDelete("/{photoId:guid}", async (Guid photoId, ClaimsPrincipal user, IHostPhotoService service, CancellationToken cancellationToken) =>
    await service.DeleteAsync(OwnerId(user), photoId, cancellationToken) ? Results.NoContent() : Results.NotFound());

var publicQr = app.MapGroup("/api/public/qr").RequireRateLimiting("public-qr");
publicQr.MapGet("/{token}", async (string token, IPublicQrService service, CancellationToken cancellationToken) => Results.Ok(await service.GetAsync(token, cancellationToken)));
publicQr.MapPost("/{token}/reservations", async (string token, ReserveUploadRequest request, IPublicQrService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ReserveAsync(token, new ReserveUploadCommand(request.FileName, request.MimeType, request.FileSize, request.IdempotencyKey), cancellationToken)));
publicQr.MapPost("/{token}/reservations/{reservationId:guid}/upload-url", async (string token, Guid reservationId, IPublicQrService service, CancellationToken cancellationToken) => Results.Ok(await service.PrepareUploadAsync(token, reservationId, cancellationToken)));
publicQr.MapPost("/{token}/reservations/{reservationId:guid}/complete", async (string token, Guid reservationId, IPublicQrService service, CancellationToken cancellationToken) => Results.Ok(await service.CompleteUploadAsync(token, reservationId, cancellationToken)));
publicQr.MapPost("/{token}/reservations/{reservationId:guid}/cancel", async (string token, Guid reservationId, IPublicQrService service, CancellationToken cancellationToken) => { await service.CancelAsync(token, reservationId, cancellationToken); return Results.NoContent(); });

app.Run();

static Task SignInAsync(HttpContext context, Bizden.Domain.Entities.HostUser user)
{
    var identity = new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email)
    ], CookieAuthenticationDefaults.AuthenticationScheme);
    return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}

static IResult AuthError(string code, int statusCode) => Results.Json(new { code, message = "Authentication request could not be completed." }, statusCode: statusCode);
static IResult ValidationError(string message) => Results.BadRequest(new { code = "VALIDATION_ERROR", message });
static Guid OwnerId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

public sealed record RegisterHostRequest(string Name, string Email, string Password);
public sealed record LoginHostRequest(string Email, string Password);
public sealed record VerifyHostEmailRequest(string Email, string Code);
public sealed record ResendVerificationRequest(string Email);
public sealed record HostSessionResponse(Guid Id, string Name, string Email);
public sealed record CreateEventRequest(string Name, string? Description, DateTimeOffset EventDate, string TimeZone, DateTimeOffset UploadStartAt, DateTimeOffset UploadEndAt, EventStatus Status);
public sealed record UpdateEventRequest(string Name, string? Description, DateTimeOffset EventDate, string TimeZone, DateTimeOffset UploadStartAt, DateTimeOffset UploadEndAt, EventStatus Status);
public sealed record CreateInvitationRequest(string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, int Count = 1);
public sealed record UpdateInvitationRequest(string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, bool IsActive);
public sealed record ReserveUploadRequest(string FileName, string MimeType, long FileSize, string IdempotencyKey);
public partial class Program;
