using FluentValidation;
using HealthTrace.API.Services;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Validations;
using HealthTrace.DAL;
using HealthTrace.DAL.Data;
using HealthTrace.DAL.Repositories;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.DAL.Storage;
using HealthTrace.PL.API.Configurations;
using HealthTrace.PL.API.Handlers;
using HealthTrace.PL.API.Handlers.Interfaces;
using HealthTrace.PL.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilogLogging(builder.Configuration, builder.Environment);

// Add services to the container.

builder.Services.AddDbContext<HealthTraceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HealthTraceDb")
    )
);

builder.Services.AddOptions<FileLoggingOptions>()
    .Bind(builder.Configuration.GetSection(FileLoggingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterModelValidator>();
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<IErrorDetailsMapper, ExceptionStatusMapper>();

var app = builder.Build();

app.UseCors("AllowAngularDev");

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api-docs");
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();