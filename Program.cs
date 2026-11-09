using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ZuMarket;
using ZuMarket.Models;
using ZuMarket.Services;
using ZuMarket.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(2));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<IStringLocalizerFactory, EmbeddedResourceStringLocalizerFactory>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddScoped<IPrivateFileStorage, PrivateFileStorage>();
builder.Services.AddScoped<IListingSearchService, ListingSearchService>();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 16 * 1024);
builder.Services.Configure<AiReviewOptions>(builder.Configuration.GetSection(AiReviewOptions.SectionName));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.AddHttpClient<IAiListingReviewProvider, OpenAiListingReviewProvider>();
builder.Services.AddScoped<IListingReviewOrchestrator, ListingReviewOrchestrator>();
builder.Services.Configure<AiAssistOptions>(builder.Configuration.GetSection(AiAssistOptions.SectionName));
builder.Services.AddHttpClient<IAiResponsesClient, OpenAiResponsesClient>();
builder.Services.AddScoped<IListingDraftService, ListingDraftService>();
builder.Services.AddScoped<IListingTranslationService, ListingTranslationService>();
builder.Services.AddScoped<IReportSummaryService, ReportSummaryService>();
builder.Services.AddHostedService<ListingTranslationWorker>();
var draftDailyLimit = Math.Max(1, builder.Configuration.GetValue<int?>($"{AiAssistOptions.SectionName}:{nameof(AiAssistOptions.DraftDailyLimit)}") ?? 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ListingDraftRateLimit.PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = draftDailyLimit, Window = TimeSpan.FromDays(1), QueueLimit = 0 }));
});
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IUniversityEmailPolicy, UniversityEmailPolicy>();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<IdentitySetupService>();

var app = builder.Build();

if (args.Contains("--migrate-database", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    return;
}

if (args.Contains("--check-ai-provider", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine("AI_PROVIDER_STATUS=DisabledOutsideDevelopment");
        return;
    }
    await using var scope = app.Services.CreateAsyncScope();
    var provider = scope.ServiceProvider.GetRequiredService<IAiListingReviewProvider>();
    Console.WriteLine("AI_PROVIDER=OpenAI");
    Console.WriteLine($"AI_PROVIDER_ENABLED={provider.IsEnabled}");
    Console.WriteLine($"AI_API_KEY_CONFIGURED={provider.HasApiKey}");
    Console.WriteLine($"AI_REQUIRED_SETTINGS_CONFIGURED={provider.HasRequiredSettings}");
    var availability = await provider.CheckAvailabilityAsync();
    Console.WriteLine($"AI_PROVIDER_STATUS={availability}");
    return;
}

if (args.Contains("--check-ai-configuration", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine("AI_CONFIGURATION_STATUS=DisabledOutsideDevelopment");
        return;
    }
    await using var scope = app.Services.CreateAsyncScope();
    var provider = scope.ServiceProvider.GetRequiredService<IAiListingReviewProvider>();
    Console.WriteLine("AI_PROVIDER=OpenAI");
    Console.WriteLine($"AI_PROVIDER_ENABLED={provider.IsEnabled}");
    Console.WriteLine($"AI_API_KEY_CONFIGURED={provider.HasApiKey}");
    Console.WriteLine($"AI_REQUIRED_SETTINGS_CONFIGURED={provider.HasRequiredSettings}");
    Console.WriteLine("AI_PROVIDER_STATUS=NotChecked");
    return;
}

if (args.Contains("--inspect-database", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine("DATABASE_INSPECTION=DisabledOutsideDevelopment");
        return;
    }
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.OpenConnectionAsync();
    await using var command = database.Database.GetDbConnection().CreateCommand();
    command.CommandText = "SELECT @@SERVERNAME, DB_NAME(), CASE WHEN COL_LENGTH(N'dbo.Listings', N'ReviewState') IS NULL THEN 0 ELSE 1 END";
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    var serverName = reader.GetString(0);
    var databaseName = reader.GetString(1);
    var reviewStateExists = reader.GetInt32(2) == 1;
    await reader.CloseAsync();
    var applied = await database.Database.GetAppliedMigrationsAsync();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInspection");
    var inspection = new[]
    {
        $"DATABASE_SERVER={serverName}",
        $"DATABASE_NAME={databaseName}",
        $"REVIEWSTATE_COLUMN={(reviewStateExists ? "PRESENT" : "MISSING")}",
        $"ADD_AI_LISTING_REVIEW_APPLIED={applied.Contains("20261109090000_AddAiListingReview")}"
    };
    foreach (var line in inspection)
    {
        Console.WriteLine(line);
        logger.LogInformation("{InspectionResult}", line);
    }
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

var cultures = new[] { new CultureInfo("en"), new CultureInfo("ar") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures,
    RequestCultureProviders = new IRequestCultureProvider[] { new CookieRequestCultureProvider() }
});

app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHub<ChatHub>("/chat-hub");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
