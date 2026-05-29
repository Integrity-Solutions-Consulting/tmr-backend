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
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

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

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseInMemoryDatabase("TmrDb"));

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService,   TokenService>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<ChangePasswordHandler>();
builder.Services.AddScoped<GetCurrentUserHandler>();
builder.Services.AddScoped<GetPermissionsHandler>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditInterceptor>();

// ── Authorization Handler (Fase 6) ────────────────────────
builder.Services.AddScoped<IAuthorizationHandler, HasModulePermissionHandler>();

// ── Memory Cache para blacklist de tokens ──────────────────
builder.Services.AddMemoryCache();

// ── Health Check ──────────────────────────────────────────
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

// Register FluentValidation validators from the auth feature
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ── JWT Middleware ─────────────────────────────────────────
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt.Issuer,
            ValidAudience            = jwt.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew                = TimeSpan.Zero
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

// ── Authorization Policies (Fase 6) ──────────────────────
builder.Services.AddAuthorization(options =>
{
    // TimeReport Module
    options.AddPolicy("CanViewTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "VIEW")));
    
    options.AddPolicy("CanCreateTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "CREATE")));
    
    options.AddPolicy("CanEditTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "EDIT")));
    
    options.AddPolicy("CanDeleteTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "DELETE")));
    
    // Proyectos Module
    options.AddPolicy("CanViewProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "VIEW")));
    
    options.AddPolicy("CanCreateProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "CREATE")));
    
    options.AddPolicy("CanEditProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "EDIT")));
    
    options.AddPolicy("CanDeleteProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "DELETE")));
    
    // Usuarios Module
    options.AddPolicy("CanViewUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "VIEW")));
    
    options.AddPolicy("CanCreateUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "CREATE")));
    
    options.AddPolicy("CanEditUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "EDIT")));
    
    options.AddPolicy("CanDeleteUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "DELETE")));
    
    // Reportes Module
    options.AddPolicy("CanViewReportes", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Reportes", "VIEW")));
    
    options.AddPolicy("CanCreateReportes", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Reportes", "CREATE")));
    
    options.AddPolicy("CanEditReportes", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Reportes", "EDIT")));
    
    options.AddPolicy("CanDeleteReportes", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Reportes", "DELETE")));
    
    // Dashboard Module
    options.AddPolicy("CanViewDashboard", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Dashboard", "VIEW")));
    
    // Administración Module
    options.AddPolicy("CanViewAdministracion", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Administracion", "VIEW")));
    
    options.AddPolicy("CanCreateAdministracion", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Administracion", "CREATE")));
    
    options.AddPolicy("CanEditAdministracion", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Administracion", "EDIT")));
    
    options.AddPolicy("CanDeleteAdministracion", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Administracion", "DELETE")));
    
    // Auditoría Module
    options.AddPolicy("CanViewAuditoria", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Auditoria", "VIEW")));
});
var app = builder.Build();

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

// ── Middleware de Autenticación y Autorización ─────────────
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapClientesEndpoints();
app.MapAuthEndpoints();
app.MapUsuariosEndpoints();
app.MapCargaActividadesEndpoints();
app.MapColaboradoresEndpoints();
app.MapConfiguracionEndpoints();
app.MapDashboardEndpoints();
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapReportesEndpoints();
app.MapTimeReportEndpoints();

app.Run();
