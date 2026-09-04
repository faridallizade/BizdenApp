using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Bizden.Application.Authentication;
using Bizden.Application.Events;
using Bizden.Application.Invitations;
using Bizden.Application.PublicAccess;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddHealthChecks();
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
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options => options.AddPolicy("host-auth", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    })));

var app = builder.Build();
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Bizdən API", status = "ready" }));
app.MapHealthChecks("/health");

var auth = app.MapGroup("/api/host/auth").RequireRateLimiting("host-auth");
auth.MapPost("/register", async (RegisterHostRequest request, IHostAuthenticationService service, HttpContext context, CancellationToken cancellationToken) =>
{
    var result = await service.RegisterAsync(new RegisterHostCommand(request.Name, request.Email, request.Password), cancellationToken);
    if (!result.Succeeded) return AuthError(result.ErrorCode!, StatusCodes.Status400BadRequest);
    await SignInAsync(context, result.User!);
    return Results.Created("/api/host/auth/me", new HostSessionResponse(result.User!.Id, result.User.Name, result.User.Email));
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

var publicQr = app.MapGroup("/api/public/qr");
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
public sealed record HostSessionResponse(Guid Id, string Name, string Email);
public sealed record CreateEventRequest(string Name, string? Description, DateTimeOffset EventDate, string TimeZone, DateTimeOffset UploadStartAt, DateTimeOffset UploadEndAt, EventStatus Status);
public sealed record UpdateEventRequest(string Name, string? Description, DateTimeOffset EventDate, string TimeZone, DateTimeOffset UploadStartAt, DateTimeOffset UploadEndAt, EventStatus Status);
public sealed record CreateInvitationRequest(string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, int Count = 1);
public sealed record UpdateInvitationRequest(string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, bool IsActive);
public sealed record ReserveUploadRequest(string FileName, string MimeType, long FileSize, string IdempotencyKey);
public partial class Program;
