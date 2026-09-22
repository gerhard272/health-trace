using FluentValidation;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Validations;
using HealthTrace.DAL.Data;
using HealthTrace.DAL.Repositories;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.PL.API.Configurations;
using HealthTrace.PL.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<HealthTraceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HealthTraceDb")
    )
);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterModelValidator>();
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();