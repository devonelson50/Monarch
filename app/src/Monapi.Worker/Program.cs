using Monapi.Worker;

/// Devon Nelson
/// service worker entrypoint

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
