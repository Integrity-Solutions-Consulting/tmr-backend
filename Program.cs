using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Clientes;
using tmr_backend.Features.Auth;
using tmr_backend.Features.CargaActividades;
using tmr_backend.Features.Colaboradores;
using tmr_backend.Features.Configuracion;
using tmr_backend.Features.Dashboard;
using tmr_backend.Features.Lideres;
using tmr_backend.Features.Proyectos;
using tmr_backend.Features.Reportes;
using tmr_backend.Features.TimeReport;
using Scalar.AspNetCore;
using tmr_backend.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
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

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseInMemoryDatabase("TmrDb"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPasswordHasher,   PasswordHasher>();
builder.Services.AddScoped<ITokenService,     TokenService>();
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<ILiderService,     LiderService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Register FluentValidation validators from the auth feature
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ── JWT Middleware ─────────────────────────────────────────
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
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

// Los permisos granulares (PROYECTOS_CREATE, etc.) los genera PermissionPolicyProvider
// dinámicamente a partir del claim "permission" inyectado por PermissionEnrichmentMiddleware.
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

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
        {
            PreferredSecuritySchemes = ["Bearer"]
        };
    });
}

app.UseHttpsRedirection();

app.MapClientesEndpoints();
app.MapAuthEndpoints();
app.MapCargaActividadesEndpoints();
app.MapColaboradoresEndpoints();
app.MapConfiguracionEndpoints();
app.MapDashboardEndpoints();
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapReportesEndpoints();
app.MapTimeReportEndpoints();

app.Run();
