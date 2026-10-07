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
using tmr_backend.Features.Colaboradores.DTOs.Request;
using tmr_backend.Features.Colaboradores.Services;
using tmr_backend.Features.Colaboradores.Validators;
using tmr_backend.Features.Configuracion;
using tmr_backend.Features.Dashboard;
using tmr_backend.Features.Lideres;
using tmr_backend.Features.Proyectos;
using tmr_backend.Features.Catalogos;
using tmr_backend.Features.Reportes;
using tmr_backend.Features.Reportes.Carbone;
using tmr_backend.Features.Reportes.Services;
using tmr_backend.Features.TimeReport;
using tmr_backend.Features.HealthCheck.Services;
using tmr_backend.Features.Configuracion.Usuarios.Application;
using tmr_backend.Features.Configuracion.Usuarios.Endpoints;
using tmr_backend.Features.Configuracion.Roles.Application;
using tmr_backend.Features.Configuracion.Roles.Endpoints;
using tmr_backend.Features.Configuracion.DiasFestivos.Application;
using tmr_backend.Features.Configuracion.DiasFestivos.Endpoints;
using tmr_backend.Features.Configuracion.Catalogos.Application;
using tmr_backend.Features.Configuracion.Catalogos.Endpoints;
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
using tmr_backend.Features.Auth.DTOs.Request;
using tmr_backend.Features.Lideres.Services;
using tmr_backend.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using tmr_backend.Shared.Middleware;
using Microsoft.OpenApi;
using tmr_backend.Shared;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using tmr_backend.Infrastructure.Extensions;
using tmr_backend.Infrastructure.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES CONFIGURATION
// =======================

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
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(defaultConnection)
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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});


// ── Memory Cache & HttpContext ──
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// ── Core Security & JWT Settings ──
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();

// ── Background Services ──
builder.Services.Configure<SessionCleanupSettings>(builder.Configuration.GetSection("SessionCleanup"));
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.Configure<WeeklyNotificationSettings>(builder.Configuration.GetSection("WeeklyNotificationSettings"));
builder.Services.AddHostedService<WeeklyNotificationService>();



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
builder.Services.AddHttpClient(); // sm - IHttpClientFactory para importar feriados (DiasFestivosService)
builder.Services.AddScoped<ICatalogosConfigService, CatalogosConfigService>();

// Feature: Carga Actividades
builder.Services.AddScoped<ICargarActividadesExcelHandler, CargarActividadesExcelHandler>();

// Feature: HealthCheck
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

// Feature: Reportes con plantillas (Carbone). El microservicio solo lo consume el backend.
builder.Services.Configure<CarboneSettings>(builder.Configuration.GetSection("Carbone"));
builder.Services.AddHttpClient<ICarboneClient, CarboneClient>((sp, client) =>
{
    var carbone = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CarboneSettings>>().Value;
    client.BaseAddress = new Uri(carbone.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(carbone.TimeoutSeconds);
});
builder.Services.AddScoped<ISeguimientoReporteService, SeguimientoReporteService>();

// ── Fluent Validation ──
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ================================================================
// VALIDADORES DE COLABORADORES (nuevo)
// ================================================================
builder.Services.AddScoped<IValidator<RegistrarSalidaRequest>, RegistrarSalidaRequestValidator>();

// ── Authentication & JWT Setup ──
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
if (string.IsNullOrWhiteSpace(jwt.SecretKey) || jwt.SecretKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey debe configurarse fuera del repositorio y tener al menos 32 caracteres.");

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
builder.Services.AddAuthorization(options =>
{
    // Los endpoints públicos deben declararse explícitamente con AllowAnonymous.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// =========================
// PIPELINE CONFIGURATION
// =========================

var app = builder.Build();

app.UseResponseCompression();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    if (!app.Environment.IsDevelopment())
        context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseMiddleware<JwtBlacklistMiddleware>();

app.UseMiddleware<PermissionEnrichmentMiddleware>();

app.UseRateLimiter();
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
app.MapDashboardEjecutivoEndpoints(); // sm - Dashboard ejecutivo (requerimiento Dashboard Time Report)
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapCatalogosEndpoints();
app.MapReportesEndpoints();
app.MapReportesRenderEndpoints();
app.MapTimeReportEndpoints();
app.MapUsuariosConfigEndpoints();
app.MapRolesConfigEndpoints();
app.MapDiasFestivosEndpoints();
app.MapCatalogosConfigEndpoints();

app.Run();

