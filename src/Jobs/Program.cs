using Jobs.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobsInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseJobsDashboard();

app.Run();