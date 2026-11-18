using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Security;

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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TuitionQueryService>();
builder.Services.AddScoped<MobileQueryLimiter>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TuitionAdminService>();
builder.Services.AddScoped<TuitionImportService>();
builder.Services.AddScoped<UnpaidReportService>();

builder.Services.AddTuitionAuthentication(builder.Configuration);
builder.Services.AddApiProblemDetails();

builder.Services.AddControllers();

// URL-segment versioning: /api/v1/... ; responses carry the api-supported-versions header.
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddTuitionSwagger();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Swagger UI at /swagger (also routed through Kong), OpenAPI JSON at /swagger/v1/swagger.json.
app.UseTuitionSwagger();

app.UseAuthentication();
app.UseAuthorization();

// Liveness probe used by Docker, Azure Container Apps and the smoke tests.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tuition-api" }))
    .AllowAnonymous()
    .ExcludeFromDescription();

app.MapControllers();

app.Run();
