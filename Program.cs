using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Clientes;
using tmr_backend.Features.Auth;
using tmr_backend.Features.CargaActividades; // Tu feature
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("TmrDb"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

//app.UseHttpsRedirection();

// ==========================================
// CORRECCIÓN: MIDDLEWARE DE CORS CONFIGURADO
// ==========================================
app.UseCors("PermitirAngular");

// Aquí es donde tu arquitectura expone los endpoints automáticamente
app.MapClientesEndpoints();
app.MapAuthEndpoints();
app.MapCargaActividadesEndpoints(); // <-- Este método llamará a tu lógica
app.MapColaboradoresEndpoints();
app.MapConfiguracionEndpoints();
app.MapDashboardEndpoints();
app.MapLideresEndpoints();
app.MapProyectosEndpoints();
app.MapReportesEndpoints();
app.MapTimeReportEndpoints();

app.Run();