var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Liveness probe used by Docker, Azure Container Apps and the smoke tests.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tuition-policy-mcp" }));

app.Run();
