using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));
builder.Services.Configure<PasswordHashingOptions>(builder.Configuration.GetSection("Auth"));

// PostgreSQL is the server database in every running environment (docker compose and Azure).
builder.Services.AddDbContext<TuitionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Tuition")));

builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

// Liveness probe used by Docker, Azure Container Apps and the smoke tests.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tuition-api" }));

app.Run();
