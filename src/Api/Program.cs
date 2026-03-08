using BggIntegration.Infrastructure;
using GameCollection.Application;
using GameCollection.Infrastructure;
using GameCollection.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var dbPath = builder.Configuration["Database:Path"] ?? "/data/bgci.db";
var connectionString = $"Data Source={dbPath}";

// Controllers
builder.Services.AddControllers();

// MediatR — register handlers from all application assemblies
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CollectedGameMappings).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(BggIntegration.Application.Queries.SearchBggQueryHandler).Assembly);
});

// Infrastructure
builder.Services.AddGameCollectionInfrastructure(connectionString);
builder.Services.AddBggIntegrationInfrastructure();

// CORS — allow the nginx-served frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Run DB migrations at startup
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseCors();
app.MapControllers();

app.Run();
