using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Clientes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Database (InMemory for simple testing as requested)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("TmrDb"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map Feature Endpoints
app.MapClientesEndpoints();

// TODO: Add other feature endpoints here as they are developed
// app.MapAuthEndpoints();
// app.MapProyectosEndpoints();

app.Run();
