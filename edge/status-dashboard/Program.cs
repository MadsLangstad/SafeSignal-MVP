var builder = WebApplication.CreateBuilder(args);

// Add HTTP client
builder.Services.AddHttpClient();

var app = builder.Build();

// Serve static files
app.UseDefaultFiles();
app.UseStaticFiles();

// API proxy endpoint to avoid CORS issues
app.MapGet("/api/proxy/stats", async (IHttpClientFactory factory) =>
{
    var http = factory.CreateClient();
    var policyServiceUrl = Environment.GetEnvironmentVariable("POLICY_SERVICE_URL") ?? "http://policy-service:5000";
    var response = await http.GetStringAsync($"{policyServiceUrl}/api/stats");
    return Results.Content(response, "application/json");
});

app.MapGet("/api/proxy/alerts", async (IHttpClientFactory factory, int limit = 20) =>
{
    var http = factory.CreateClient();
    var policyServiceUrl = Environment.GetEnvironmentVariable("POLICY_SERVICE_URL") ?? "http://policy-service:5000";
    var response = await http.GetStringAsync($"{policyServiceUrl}/api/alerts?limit={limit}");
    return Results.Content(response, "application/json");
});

app.Logger.LogInformation("SafeSignal Status Dashboard running on http://+:5200");
await app.RunAsync();
