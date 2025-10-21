using Monarch.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

var builder = WebApplication.CreateBuilder(args);

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<Monarch.Services.DatabaseTestService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Test all rows
app.MapGet("/db/sampleTable", (Monarch.Services.DatabaseTestService db) =>
{
    var rows = db.GetAllStatuses();
    return Results.Json(rows);
});

// Test a specific resource by name
app.MapGet("/db/sampleTable/{name}", (string name, Monarch.Services.DatabaseTestService db) =>
{
    var status = db.GetStatusByResource(name);
    return status is not null 
        ? Results.Text($"{name} is {status}")
        : Results.NotFound("Resource not found.");
});


app.Run();
