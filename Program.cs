using Microsoft.EntityFrameworkCore;
using Npgsql;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Features.Clientes;
using tmr_backend.Features.Auth;
using tmr_backend.Features.Usuarios.Endpoints;
using tmr_backend.Features.CargaActividades;

using tmr_backend.Features.Colaboradores;
using tmr_backend.Features.Dashboard;
using tmr_backend.Features.Lideres;
using tmr_backend.Features.Proyectos;
using tmr_backend.Features.Catalogos;
using tmr_backend.Features.Reportes;
using tmr_backend.Features.TimeReport;
using tmr_backend.Features.HealthCheck.Services;
using tmr_backend.Features.Configuracion.Usuarios.Application;
using tmr_backend.Features.Configuracion.Usuarios.Endpoints;
using tmr_backend.Features.Configuracion.Roles.Application;
using tmr_backend.Features.Configuracion.Roles.Endpoints;
using tmr_backend.Features.Configuracion.DiasFestivos.Application;
using tmr_backend.Features.Configuracion.DiasFestivos.Endpoints;
using tmr_backend.Features.HealthCheck.Endpoints;
using Scalar.AspNetCore;
using tmr_backend.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;
using FluentValidation;
using tmr_backend.Features.Configuracion.Register_Temp.Validators;
using tmr_backend.Features.Configuracion.Register_Temp.Services;
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.Auth.Validators;
using tmr_backend.Features.Auth.Services;
using tmr_backend.Features.Lideres.Services;
using tmr_backend.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using tmr_backend.Shared.Middleware;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    // Le dice a OpenAPI que existe un esquema Bearer JWT
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            Description = "Ingresa el token JWT. Ejemplo: eyJhbGci..."
        };

        return Task.CompletedTask;
    });
});
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// =========================
// SERVICES
// =========================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            Description  = "Ingresa el token JWT. Ejemplo: eyJhbGci..."
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
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher,   PasswordHasher>();
builder.Services.AddScoped<ITokenService,     TokenService>();
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<ILiderService,     LiderService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
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

// ── Servicios de Configuración ─────────────────────────────
builder.Services.AddScoped<IUsuariosConfigService, UsuariosConfigService>();
builder.Services.AddScoped<IRolesConfigService, RolesConfigService>();
builder.Services.AddScoped<IDiasFestivosService, DiasFestivosService>();

// ── Memory Cache para blacklist de tokens ──────────────────
builder.Services.AddMemoryCache();

// ── Health Check ──────────────────────────────────────────
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
// builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICargarActividadesExcelHandler, CargarActividadesExcelHandler>();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

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
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew                = TimeSpan.Zero
        };

        opt.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse(); // evita la respuesta default de .NET
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail(
                    401,
                    "No autorizado. Token inválido o ausente.",
                    [new ApiError("token", "El token JWT es inválido o ha expirado")]
                );
                context.HandleResponse();
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(401, "No autorizado. Token inválido o ausente.",
                    [new ApiError("token", "El token JWT es inválido o ha expirado")]);
                await context.Response.WriteAsJsonAsync(response);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode  = 403;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail(
                    403,
                    "Acceso denegado. No tienes permisos suficientes.",
                    [new ApiError("role", "Tu rol no tiene acceso a este recurso")]
                );
                var response = ApiResponse<object>.Fail(403, "Acceso denegado. No tienes permisos suficientes.",
                    [new ApiError("role", "Tu rol no tiene acceso a este recurso")]);
                await context.Response.WriteAsJsonAsync(response);
            }
        };
    });

// Los permisos granulares (PROYECTOS_CREATE, etc.) los genera PermissionPolicyProvider
// dinámicamente a partir del claim "permission" inyectado por PermissionEnrichmentMiddleware.
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// TU FEATURE
builder.Services.AddScoped<ICargarActividadesExcelHandler, CargarActividadesExcelHandler>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<JwtBlacklistMiddleware>();        // JTI blacklist — después de validar firma JWT
app.UseMiddleware<PermissionEnrichmentMiddleware>(); // Carga permisos del usuario desde caché/BD
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title                  = "TMR Backend API";
        options.Theme                  = ScalarTheme.Purple; // o el que uses
        options.DefaultHttpClient      = new(ScalarTarget.Http, ScalarClient.Http11);
        options.Authentication         = new ScalarAuthenticationOptions
        options.AddPreferredSecuritySchemes("Bearer")
               .AddHttpAuthentication("Bearer", http => { });
        options.Title                 = "TMR Backend API";
        options.Theme                 = ScalarTheme.Purple;
        options.DefaultHttpClient     = new(ScalarTarget.Http, ScalarClient.Http11);
        options.Authentication        = new ScalarAuthenticationOptions
        {
            PreferredSecuritySchemes = ["Bearer"]
        };
    });
}

app.UseHttpsRedirection();

// CORS
app.UseCors("PermitirAngular");
app.UseCors("Frontend");

// Middleware de Autenticación y Autorización
app.UseAuthentication();
app.UseCors("Frontend");
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<JwtBlacklistMiddleware>();
app.UseMiddleware<PermissionEnrichmentMiddleware>();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
// Endpoints
app.MapClientesEndpoints();
app.MapAuthEndpoints();
app.MapUsuariosEndpoints();
app.MapCargaActividadesEndpoints();
app.MapColaboradoresEndpoints();
app.MapDashboardEndpoints();
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapCatalogosEndpoints();
app.MapReportesEndpoints();
app.MapTimeReportEndpoints();
app.MapUsuariosConfigEndpoints();
app.MapRolesConfigEndpoints();
app.MapDiasFestivosEndpoints();

app.Run();