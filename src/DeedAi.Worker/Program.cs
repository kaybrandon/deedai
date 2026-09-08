using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDeedAiInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OcrQueueWorker>();

var host = builder.Build();
await host.RunAsync();
