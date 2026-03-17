using Monarch.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Monarch.Services;

/// Devon Nelson
/// Blazor WebApp entrypoint accomplishes the following:
///     - Injest OIDC configuration from appsettings.json
///     - Forward unauthenticated clients to KeyCloak for authentication
///     - Complete OIDC user authorization flow
///     - Begin the authenticated user's session
///     - Determine the authenticated user's access level based on assigned role(s)

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

builder.Services.AddScoped<AppLoadService>(sp =>
    new AppLoadService(monapiConnectionString, monarchConnectionString));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient for services that need it
HttpClient httpClient = new HttpClient();
builder.Services.AddSingleton(httpClient);

// Register Slack Admin Service for admin panel
builder.Services.AddScoped<Monarch.Services.SlackAdminService>();

// Register App Admin Service for application configuration management
builder.Services.AddScoped<Monarch.Services.AppAdminService>();

// enable forwarding to support traefik reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Configure OIDC authentication with cookie-based sessions
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
    options.GetClaimsFromUserInfoEndpoint = true;

    options.Scope.Clear();
    if (!string.IsNullOrEmpty(scopeString))
    {
        foreach (var scope in scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }        
    }
    options.CallbackPath = new Uri(redirectUri).LocalPath;

    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        NameClaimType = "preferred_username",
        RoleClaimType = ClaimTypes.Role,
        ValidIssuers = new List<string>
        {
            authority.Replace("monarch", "Monarch", StringComparison.OrdinalIgnoreCase),
            publicAuthority.Replace("monarch", "Monarch", StringComparison.OrdinalIgnoreCase)
        }
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
        },
        // convert roles from realm_access claim into individual role claims
        OnTokenValidated = context =>
        {
            var identity = context.Principal?.Identity as ClaimsIdentity;
            if (identity != null)
            {
                var realmAccessClaim = identity.FindFirst("realm_access");
                if (realmAccessClaim != null)
                {
                    using var jsonDoc = JsonDocument.Parse(realmAccessClaim.Value);
                    if (jsonDoc.RootElement.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            var roleName = role.GetString();
                            if (!string.IsNullOrEmpty(roleName))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                            }
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

// Define role-based authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Monarch Administrator"));
    // User policy may become relevant if limited guest access is needed
    // options.AddPolicy("User", policy => policy.RequireRole("Monarch User", "Monarch Administrator"));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseForwardedHeaders();
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

// Handle /logout POST requests, invalidate current session
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
});

app.Run();
