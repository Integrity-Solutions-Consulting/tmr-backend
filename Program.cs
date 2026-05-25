using dotenv.net;
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

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

var dbConnection = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:SecretKey"];

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"];

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"];

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dbConnection));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService,   TokenService>();
builder.Services.AddScoped<IAuthService,    AuthService>();

// ── Lideres ───────────────────────────────────────────────
builder.Services.AddScoped<ILiderService,   LiderService>();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ── JWT Middleware ─────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret!)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
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