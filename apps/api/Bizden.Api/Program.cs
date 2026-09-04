var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Bizdən API", status = "ready" }));
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
