using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Mapping;
using HealthTrace.DAL;
using HealthTrace.DAL.Data;
using HealthTrace.DAL.Repositories;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.DAL.Storage;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthTrace.Functions;
using Microsoft.Extensions.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddDbContext<HealthTraceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HealthTraceDb")));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<ICurrentUserService, NullCurrentUserService>();

builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName));
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

builder.Services.AddScoped<IPdfGenerator, PdfGenerator>();
builder.Services.AddScoped<IExportService, ExportService>();

builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);
builder.Build().Run();