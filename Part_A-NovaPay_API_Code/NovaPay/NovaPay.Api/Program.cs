using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NovaPay.Api.Auth;
using NovaPay.Api.Common;
using NovaPay.Api.Data;
using NovaPay.Api.Middleware;
using NovaPay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NovaPayOptions>(builder.Configuration.GetSection(NovaPayOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NovaPay API", Version = "v1" });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "token",
        In = ParameterLocation.Header,
        Description = "Enter the bearer token, e.g. \"novapay-dev-token\" (see appsettings.json NovaPay:BearerToken).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", bearerScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { bearerScheme, [] } });
});

builder.Services.AddAuthentication(StaticBearerTokenAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, StaticBearerTokenAuthHandler>(StaticBearerTokenAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("NovaPay")
    ?? "Data Source=novapay.db;Default Timeout=30";
builder.Services.AddDbContext<NovaPayDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WalletLock>();
builder.Services.AddScoped<IWalletService, WalletService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NovaPayDbContext>();
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("already exists"))
    {
        // Benign race: another instance/process won the first-time schema creation against the
        // same database file concurrently. The desired end state (schema present) already holds.
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger stays on in every environment (including the public Render deployment) so the API is
// self-documenting/testable from a browser without needing a separate Postman collection.
app.UseSwagger();
app.UseSwaggerUI();

// Render (and most PaaS hosts) terminate TLS at their edge and forward plain HTTP internally;
// redirecting to https in that setup would either loop or be redundant, so this only applies to
// local development where Kestrel itself serves both http and https.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
