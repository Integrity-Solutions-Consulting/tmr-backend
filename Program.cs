using Microsoft.EntityFrameworkCore;
using Npgsql;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Features.Clientes;
using tmr_backend.Features.Clientes.DTOs.Request;
using tmr_backend.Features.Clientes.Services;
using tmr_backend.Features.Clientes.Validators;
using tmr_backend.Features.Auth;
using tmr_backend.Features.CargaActividades;
using tmr_backend.Features.Colaboradores;
using tmr_backend.Features.Colaboradores.Services;
using tmr_backend.Features.Configuracion;
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
using tmr_backend.Infrastructure.Shared;
using tmr_backend.Features.Auth.Register;
using tmr_backend.Features.Auth.Validators;
using tmr_backend.Features.Auth.Services;
using tmr_backend.Features.Lideres.Services;
using tmr_backend.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using tmr_backend.Shared.Middleware;
using Microsoft.OpenApi;
using tmr_backend.Shared;
using System.Text.Json.Serialization;
using tmr_backend.Infrastructure.Extensions;
using tmr_backend.Infrastructure.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES CONFIGURATION
// =========================

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── JSON Serializer Configuration ──
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new DateOnlyJsonConverter());
});

// ── OpenAPI / Swagger ──
builder.Services.AddOpenApi(options =>
{
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

// ── Database Context ──
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

// ── CORS ──
 String[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});


// ── Memory Cache & HttpContext ──
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ── Core Security & JWT Settings ──
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Background Services ──
builder.Services.Configure<SessionCleanupSettings>(builder.Configuration.GetSection("SessionCleanup"));
builder.Services.AddHostedService<SessionCleanupService>();



// Feature: Clientes
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IValidator<CrearClienteRequest>, CrearClienteRequestValidator>();
builder.Services.AddScoped<IValidator<ActualizarClienteRequest>, ActualizarClienteRequestValidator>();

// Feature: Colaboradores
builder.Services.AddScoped<IColaboradorService, ColaboradorService>();
builder.Services.AddScoped<ICodigoEmpleadoGenerator, CodigoEmpleadoGenerator>();

// Feature: Líderes
builder.Services.AddScoped<ILiderService, LiderService>();

// Feature: Configuración
builder.Services.AddScoped<IUsuariosConfigService, UsuariosConfigService>();
builder.Services.AddScoped<IRolesConfigService, RolesConfigService>();
builder.Services.AddScoped<IDiasFestivosService, DiasFestivosService>();

// Feature: Carga Actividades
builder.Services.AddScoped<ICargarActividadesExcelHandler, CargarActividadesExcelHandler>();

// Feature: HealthCheck
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

// ── Fluent Validation ──
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ── Authentication & JWT Setup ──
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
                context.HandleResponse();
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail(
                    401,
                    "No autorizado. Token inválido o ausente.",
                    [new ApiError("token", "El token JWT es inválido o ha expirado")]
                );
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
                await context.Response.WriteAsJsonAsync(response);
            }
        };
    });

// ── Authorization Provider ──
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// =========================
// PIPELINE CONFIGURATION
// =========================

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

// app.UseCors("PermitirFrontend");

app.UseAuthentication();

app.UseMiddleware<JwtBlacklistMiddleware>();

app.UseMiddleware<PermissionEnrichmentMiddleware>();

app.UseCors("AllowFrontend");
app.UseAuthorization();

// ── Scalar API Reference ──
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title                  = "TMR Backend API";
        options.Theme                  = ScalarTheme.Purple;
        options.DefaultHttpClient      = new(ScalarTarget.Http, ScalarClient.Http11);
        options.Authentication         = new ScalarAuthenticationOptions
        {
            PreferredSecuritySchemes = ["Bearer"]
        };
    });
}

// ── Endpoint Mapping ──
app.MapHealthCheckEndpoints();
app.MapClientesEndpoints();
app.MapAuthEndpoints();
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

// ── Seed Template Data (Desarrollo) ──
/*if (app.Environment.IsDevelopment())
{
    try
    {
        await app.SeedTemplateDataAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Seeding skipped: {ex.Message}");
    }
}*/

app.Run();
