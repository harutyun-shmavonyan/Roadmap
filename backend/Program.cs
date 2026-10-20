using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Roadmap.Api.Auth;
using Roadmap.Api.Data;
using Roadmap.Api.Endpoints;
using Roadmap.Api.Mcp;
using Roadmap.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Allow large uploads (article images, CV PDFs) on both the multipart endpoints and the /mcp
// JSON-RPC body (where images ride as base64, ~33% larger). Kestrel's default cap is 30 MB;
// raise it so full-resolution photos fit. Per-endpoint checks still enforce per-file limits.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64L * 1024 * 1024);

// Auth config from env vars
var authPassword = Environment.GetEnvironmentVariable("AUTH_PASSWORD") ?? "changeme";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "roadmap-dev-secret-key-min-32-chars!!";
// If set, /mcp requires it: "Authorization: Bearer <secret>" or "X-Api-Key: <secret>" — the two
// header shapes a claude.ai custom connector can send. Unset = /mcp is open to anyone with the URL.
var mcpSecret = Environment.GetEnvironmentVariable("MCP_SECRET");

// Support DATABASE_URL env var (Railway Postgres format)
var connStr = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(connStr))
{
    if (connStr.StartsWith("postgres://") || connStr.StartsWith("postgresql://"))
    {
        var uri = new Uri(connStr);
        var userInfo = uri.UserInfo.Split(':');
        connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=5;Connection Idle Lifetime=30;Connection Pruning Interval=10";
    }
}
else
{
    connStr = builder.Configuration.GetConnectionString("Default");
}

builder.Services.AddDbContext<RoadmapDbContext>(options => options.UseNpgsql(connStr));

// HttpClient for server-side image fetches (add_article_image_from_url MCP tool).
builder.Services.AddHttpClient();

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly(typeof(RoadmapMcpTools).Assembly);

// JWT Authentication. TokenIssuer holds the one signing key; the password login and the
// issue_api_token MCP tool both mint through it, so their tokens are interchangeable on /api.
var issuer = new TokenIssuer(jwtSecret);
builder.Services.AddSingleton(issuer);
var key = issuer.Key;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // Read token from cookie if no Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    context.Token = context.Request.Cookies["roadmap_token"];
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RoadmapDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedDemoRoadmap(db);
    await SeedData.SeedMeals(db);
}

app.UseCors();

// Serve frontend static files. index.html must never be cached: it names the hashed bundle, and
// a browser holding an old copy keeps loading the old app after a deploy (seen 2026-10-19). The
// hashed assets, by contrast, change name whenever their content does, so they can be cached for
// a year and marked immutable.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
            headers.CacheControl = "public, max-age=31536000, immutable";
        else
            headers.CacheControl = "no-cache";
    }
};
app.UseDefaultFiles();
app.UseStaticFiles(staticFiles);

app.UseAuthentication();
app.UseAuthorization();

// === Auth endpoints (no auth required) ===
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    if (!FixedTimeEquals(req.Password, authPassword))
        return Results.Unauthorized();

    var (token, expiresAt) = issuer.Issue();
    return Results.Ok(new { token, expiresAt });
}).AllowAnonymous();

app.MapGet("/api/auth/check", (HttpContext ctx) =>
{
    return ctx.User.Identity?.IsAuthenticated == true
        ? Results.Ok(new { authenticated = true })
        : Results.Unauthorized();
}).RequireAuthorization();

// All roadmap endpoints require auth
app.MapRoadmapEndpoints();
app.MapCourseEndpoints();
app.MapExperienceEndpoints();
app.MapFoodLogEndpoints();
app.MapFlashcardEndpoints();
app.MapSignalEndpoints();

// MCP endpoint — protected by MCP_SECRET if configured. The secret is never read from the URL:
// query strings end up in logs and proxies, and the MCP spec forbids tokens there.
app.MapMcp("/mcp").AddEndpointFilter(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(mcpSecret))
    {
        var h = ctx.HttpContext.Request.Headers;
        var bearer = h.Authorization.ToString();
        var presented = bearer.StartsWith("Bearer ", StringComparison.Ordinal) ? bearer[7..] : h["X-Api-Key"].ToString();
        if (!FixedTimeEquals(presented, mcpSecret))
            return Results.Unauthorized();
    }
    return await next(ctx);
});

// SPA fallback — any non-API, non-file request serves index.html
app.MapFallbackToFile("index.html", staticFiles); // same headers on the SPA fallback

app.Run();

static bool FixedTimeEquals(string? a, string? b)
{
    if (a is null || b is null) return false;
    var x = Encoding.UTF8.GetBytes(a);
    var y = Encoding.UTF8.GetBytes(b);
    return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
}

record LoginRequest(string Password);
