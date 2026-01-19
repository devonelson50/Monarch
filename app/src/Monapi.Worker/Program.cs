using Monapi.Worker;

/// Devon Nelson
/// service worker entrypoint

var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();

host.Run();
