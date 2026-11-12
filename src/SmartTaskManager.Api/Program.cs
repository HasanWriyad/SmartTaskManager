using SmartTaskManager.Api.Services;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Infrastructure;
using SmartTaskManager.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddControllers();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();
