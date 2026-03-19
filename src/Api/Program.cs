using BggIntegration.Infrastructure;
using GameCollection.Application;
using GameCollection.Infrastructure;
using GameCollection.Infrastructure.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// MediatR — register handlers from all application assemblies
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CollectedGameMappings).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(BggIntegration.Application.Queries.SearchBggQueryHandler).Assembly);
});

// Infrastructure
builder.Services.AddGameCollectionInfrastructure(builder.Configuration);
builder.Services.AddBggIntegrationInfrastructure(builder.Configuration);

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
