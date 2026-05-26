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
using tmr_backend.Features.Catalogos;
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

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseInMemoryDatabase("TmrDb"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ── Seguridad ─────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService,   TokenService>();
builder.Services.AddScoped<IAuthService,    AuthService>();

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
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapClientesEndpoints();
app.MapAuthEndpoints();
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
