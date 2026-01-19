using Monapi.Worker;

/// Devon Nelson
/// service worker entrypoint

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// read appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var host = builder.Build();
host.Run();
