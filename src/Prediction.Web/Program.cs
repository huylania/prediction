using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Prediction.Web.ApiFootball;
using Prediction.Web.Components;
using Prediction.Web.Data;
using Prediction.Web.Services;

// Số thập phân hiển thị kiểu bảng kèo (1.95, -0.75) bất kể ngôn ngữ của máy chủ.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default.");
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(connectionString));

builder.Services.Configure<ApiFootballOptions>(builder.Configuration.GetSection(ApiFootballOptions.Section));
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.Section));
builder.Services.Configure<DisplayOptions>(builder.Configuration.GetSection(DisplayOptions.Section));
builder.Services.Configure<AiReportOptions>(builder.Configuration.GetSection(AiReportOptions.Section));
builder.Services.Configure<OpenMeteoOptions>(builder.Configuration.GetSection(OpenMeteoOptions.Section));
builder.Services.AddHttpClient<OpenMeteoClient>(client => client.Timeout = TimeSpan.FromSeconds(20));

builder.Services.AddHttpClient<ApiFootballClient>((sp, client) =>
{
    var baseUrl = builder.Configuration[$"{ApiFootballOptions.Section}:BaseUrl"] ?? new ApiFootballOptions().BaseUrl;
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TimeDisplay>();
builder.Services.AddSingleton<SuggestionService>();
builder.Services.AddSingleton<SyncCoordinator>();
builder.Services.AddSingleton<AdminAccountService>();
builder.Services.AddSingleton<DemoDataSeeder>();
builder.Services.AddSingleton<InsightService>();
builder.Services.AddSingleton<BacktestService>();
builder.Services.AddSingleton<AnalysisService>();
builder.Services.AddSingleton<AiReportService>();
builder.Services.AddSingleton<MatchContextService>();
builder.Services.AddScoped<DataSyncService>();
builder.Services.AddHostedService<SyncBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/dang-nhap";
        o.LogoutPath = "/dang-xuat";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "prediction.auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (args.Contains("--seed-demo"))
{
    var message = await app.Services.GetRequiredService<DemoDataSeeder>().SeedAsync(CancellationToken.None);
    app.Logger.LogInformation("{Message}", message);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/dang-xuat", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/dang-nhap");
});

app.Run();
