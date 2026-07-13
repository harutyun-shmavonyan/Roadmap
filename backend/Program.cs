using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Roadmap.Api.Data;
using Roadmap.Api.Endpoints;
using Roadmap.Api.Mcp;
using Roadmap.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Auth config from env vars
var authPassword = Environment.GetEnvironmentVariable("AUTH_PASSWORD") ?? "changeme";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "roadmap-dev-secret-key-min-32-chars!!";
var mcpSecret = Environment.GetEnvironmentVariable("MCP_SECRET"); // if set, required as Bearer token on /mcp

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

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly(typeof(RoadmapMcpTools).Assembly);

// JWT Authentication
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
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
}

app.UseCors();

// Serve frontend static files
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// === Auth endpoints (no auth required) ===
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    if (req.Password != authPassword)
        return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")]),
        Expires = DateTime.UtcNow.AddDays(30),
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { token = tokenString, expiresAt = tokenDescriptor.Expires });
}).AllowAnonymous();

app.MapGet("/api/auth/check", (HttpContext ctx) =>
{
    return ctx.User.Identity?.IsAuthenticated == true
        ? Results.Ok(new { authenticated = true })
        : Results.Unauthorized();
}).RequireAuthorization();

// All roadmap endpoints require auth
app.MapRoadmapEndpoints();

// MCP endpoint — protected by MCP_SECRET if configured
app.MapMcp("/mcp").AddEndpointFilter(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(mcpSecret))
    {
        var auth = ctx.HttpContext.Request.Headers.Authorization.ToString();
        var expected = $"Bearer {mcpSecret}";
        if (!string.Equals(auth, expected, StringComparison.Ordinal))
            return Results.Unauthorized();
    }
    return await next(ctx);
});

// SPA fallback — any non-API, non-file request serves index.html
app.MapFallbackToFile("index.html");

app.Run();

record LoginRequest(string Password);
