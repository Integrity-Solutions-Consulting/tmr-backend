using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Clientes;
using tmr_backend.Features.Auth;
using tmr_backend.Features.Auth.Login;
using tmr_backend.Features.Auth.Refresh;
using tmr_backend.Features.Auth.Logout;
using tmr_backend.Features.Auth.ChangePassword;
using tmr_backend.Features.Auth.GetCurrentUser;
using tmr_backend.Features.Auth.GetPermissions;
using tmr_backend.Features.Usuarios.Endpoints;
using tmr_backend.Features.CargaActividades;
using tmr_backend.Features.Colaboradores;
using tmr_backend.Features.Configuracion;
using tmr_backend.Features.Dashboard;
using tmr_backend.Features.Lideres;
using tmr_backend.Features.Proyectos;
using tmr_backend.Features.Catalogos;
using tmr_backend.Features.Reportes;
using tmr_backend.Features.TimeReport;
using tmr_backend.Features.HealthCheck.Services;
using tmr_backend.Features.HealthCheck.Endpoints;
using Scalar.AspNetCore;
using tmr_backend.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;
using FluentValidation;
using tmr_backend.Features.Configuracion.Register_Temp.Validators;
using tmr_backend.Features.Configuracion.Register_Temp.Services;
using tmr_backend.Infrastructure.Shared;

using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Ingresa el JWT Access Token"
            }
        };
        return Task.CompletedTask;
    });
}); 
// =========================
// SERVICES
// =========================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// DB (PostgreSQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService,   TokenService>();
builder.Services.AddScoped<IAuthService,     AuthService>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<ChangePasswordHandler>();
builder.Services.AddScoped<GetCurrentUserHandler>();
builder.Services.AddScoped<GetPermissionsHandler>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Memory Cache para blacklist de tokens ──────────────────
builder.Services.AddMemoryCache();

// ── Health Check ──────────────────────────────────────────
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// JWT Authentication
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // ── Validación de Blacklist en OnTokenValidated ──
        // Fase 3: Implementar blacklist cache para tokens revocados
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
                
                if (jti is not null)
                {
                    var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                    var cacheKey = $"blacklist:{jti}";
                    
                    // 1. Verificar caché primero (rendimiento)
                    if (cache.TryGetValue(cacheKey, out _))
                    {
                        ctx.Fail("Token revocado (blacklist cache)");
                        return;
                    }
                    
                    // 2. Fallback a BD si no está en caché
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                    var isBlacklisted = await db.TblAutenticacionTokenBlacklists
                        .AnyAsync(t => t.Token == jti && t.Activo);
                    
                    if (isBlacklisted)
                    {
                        // Extraer expiration para saber cuánto cachear
                        var expClaim = ctx.Principal?.FindFirstValue("exp");
                        var cacheExpiration = TimeSpan.FromMinutes(15); // Por defecto 15 min
                        
                        if (long.TryParse(expClaim, out var expUnix))
                        {
                            var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).DateTime;
                            var ttl = expDateTime - DateTime.UtcNow;
                            if (ttl > TimeSpan.Zero)
                                cacheExpiration = ttl;
                        }
                        
                        // Cachear el JTI revocado para futuras solicitudes
                        cache.Set(cacheKey, true, cacheExpiration);
                        
                        ctx.Fail("Token revocado (BD)");
                        return;
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

// TU FEATURE
builder.Services.AddScoped<ICargarActividadesExcelHandler, CargarActividadesExcelHandler>();

var app = builder.Build();

// =========================
// PIPELINE
// =========================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer")
               .AddHttpAuthentication("Bearer", http => { });
    });
}

app.UseHttpsRedirection();

// CORS
app.UseCors("PermitirAngular");
app.UseCors("Frontend");

// Middleware de Autenticación y Autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
// Endpoints
app.MapClientesEndpoints();
app.MapAuthEndpoints();
app.MapUsuariosEndpoints();
app.MapCargaActividadesEndpoints();
app.MapColaboradoresEndpoints();
app.MapConfiguracionEndpoints();
app.MapDashboardEndpoints();
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapCatalogosEndpoints();
app.MapReportesEndpoints();
app.MapTimeReportEndpoints();

app.Run();