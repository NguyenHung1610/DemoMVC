using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseSqlite(
        connectionString,
        sqlite => sqlite.MigrationsHistoryTable("__IdentityMigrationsHistory")));

builder.Services.AddControllersWithViews();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IStudentEmailSender, SmtpStudentEmailSender>();
builder.Services.AddScoped<ExcelImportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await EnsureLegacyDatabaseExistsAsync(app);
await InitializeIdentityAsync(app);

app.Run();

static async Task InitializeIdentityAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityContext>();
    await context.Database.MigrateAsync();

    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var email = configuration["StudentIdentity:DefaultAdmin:Email"] ?? "admin@student.local";
    var password = configuration["StudentIdentity:DefaultAdmin:Password"];
    if (string.IsNullOrWhiteSpace(password))
    {
        app.Logger.LogWarning(
            "Default Student admin was not seeded. Set StudentIdentity:DefaultAdmin:Password using User Secrets or an environment variable.");
        return;
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByEmailAsync(email) is not null)
    {
        return;
    }

    var admin = new ApplicationUser
    {
        UserName = email,
        Email = email,
        EmailConfirmed = true
    };
    var result = await userManager.CreateAsync(admin, password);
    if (!result.Succeeded)
    {
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Could not create the default Student admin: {errors}");
    }

    app.Logger.LogInformation("Created the default Student admin account {Email}.", email);
}

static async Task EnsureLegacyDatabaseExistsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.EnsureCreatedAsync();
}