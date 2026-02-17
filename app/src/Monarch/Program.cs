using Monarch.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Cryptography.X509Certificates;
using Monarch.Services;

/// Devon Nelson
/// Blazor WebApp entrypoint accomplishes the following:
///     - Injest OIDC configuration from appsettings.json
///     - Forward unauthenticated clients to KeyCloak for authentication
///     - Complete OIDC user authorization flow
///     - Begin the authenticated user's session

var builder = WebApplication.CreateBuilder(args);

var keycloakConfig = builder.Configuration.GetSection("Keycloak");
var authority = keycloakConfig.GetValue<string>("Authority");
var publicAuthority = keycloakConfig.GetValue<string>("PublicAuthority");
var clientId = keycloakConfig.GetValue<string>("ClientId");
var scopeString = keycloakConfig.GetValue<string>("Scope");
var redirectUri = keycloakConfig.GetValue<string>("RedirectUri");

var clientSecret = File.ReadAllText("/run/secrets/monarch_oidc_client_secret").Trim();

if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(publicAuthority) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(scopeString) || string.IsNullOrEmpty(redirectUri))
{
    Console.WriteLine("Keycloak configuration is incomplete. Please review appsettings.json. One or more properties are empty.");
    Environment.Exit(500);
}

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

var monarchKey = File.ReadAllText("/run/secrets/monarch_sql_monarch_password").Trim();
var monapiConnectionString = $"Server=sqlserver;Database=monapi;User Id=monarch;Password={monarchKey};TrustServerCertificate=False;";
var monarchConnectionString = $"Server=sqlserver;Database=monarch;User Id=monarch;Password={monarchKey};TrustServerCertificate=False;";

// Add services to the container.
builder.Services.AddScoped<AppCreationService>(sp =>
    new AppCreationService(monapiConnectionString, monarchConnectionString));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient for services that need it
HttpClient httpClient = new HttpClient();
builder.Services.AddSingleton(httpClient);

// Register Slack Admin Service for admin panel
builder.Services.AddSingleton<Monarch.Services.SlackAdminService>();

// Register App Admin Service for application configuration management
builder.Services.AddSingleton<Monarch.Services.AppAdminService>();

// enable forwarding to support traefik reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.Authority = authority;
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.RequireHttpsMetadata = true; 
    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{authority}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever(httpClient) { RequireHttps = true }
    );
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = false;

    options.Scope.Clear();
    if (!string.IsNullOrEmpty(scopeString))
    {
        foreach (var scope in scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }        
    }
    options.CallbackPath = new Uri(redirectUri).LocalPath;

    options.TokenValidationParameters.ValidIssuers = new List<string>
    {
        authority.Replace("monarch", "Monarch", StringComparison.OrdinalIgnoreCase),
        publicAuthority.Replace("monarch", "Monarch", StringComparison.OrdinalIgnoreCase)
    };

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.IssuerAddress = context.ProtocolMessage.IssuerAddress.Replace(authority, publicAuthority, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Error");
            return Task.CompletedTask;
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) && (!context.Request.Path.StartsWithSegments("/_blazor")))
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false });
    }
    await next();
});

app.UseAntiforgery();
app.Run();
