using GameCollection.Application;
using GameCollection.Infrastructure;
using GameCollection.Infrastructure.Postgres;
using Jobs.Application;
using Jobs.Infrastructure;
using Jobs.Worker;

PostgresClientSettings.Apply();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
	cfg.RegisterServicesFromAssembly(typeof(CollectedGameMappings).Assembly);
	cfg.RegisterServicesFromAssembly(typeof(BggJobPolicy).Assembly);
});

builder.Services.AddGameCollectionInfrastructure(builder.Configuration);
builder.Services.AddJobsInfrastructure(builder.Configuration, installHangfireSchema: true);
builder.Services.AddJobsWorker(builder.Configuration);

var app = builder.Build();

app.UseQaResetControl();
app.UseJobsDashboard();

app.Run();