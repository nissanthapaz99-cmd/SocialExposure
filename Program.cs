using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;
using SocialExposure.Services;

var builder = WebApplication.CreateBuilder(args);

// Use portable logging providers. The Windows Event Log provider requires
// machine-level permissions that are not available in every development setup.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add MVC
builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = DesignUploadPolicy.RequestMaxBytes);

builder.Services
    .AddDataProtection()
    .SetApplicationName("SocialExposure")
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var claimedRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

            if (!int.TryParse(idValue, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices
                .GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == userId);

            if (user == null || !user.IsActive || !user.IsApproved ||
                !string.Equals(user.Role, claimedRole, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Register services used by AccountController
builder.Services.AddScoped<OTPService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

// Create database/tables if they don't exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
    EnsureUserApprovalColumn(context);

    if (app.Environment.IsDevelopment())
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        SeedDevelopmentUser(
            context,
            passwordHasher,
            "Admin User",
            "admin@socialexposure.local",
            UserRoles.Admin,
            "Admin123!");

        SeedDevelopmentUser(
            context,
            passwordHasher,
            "Staff User",
            "staff@socialexposure.local",
            UserRoles.Staff,
            "Staff123!");

        context.SaveChanges();
    }
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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

app.Run();

static void SeedDevelopmentUser(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    string fullName,
    string email,
    string role,
    string developmentPassword)
{
    var normalizedEmail = email.Trim().ToLowerInvariant();
    if (context.Users.Any(x => x.Email.ToLower() == normalizedEmail))
        return;

    var user = new User
    {
        FullName = fullName,
        Email = normalizedEmail,
        Role = role,
        IsVerified = true,
        IsActive = true,
        IsApproved = true
    };

    user.Password = passwordHasher.HashPassword(user, developmentPassword);
    context.Users.Add(user);
}

static void EnsureUserApprovalColumn(ApplicationDbContext context)
{
    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;
    if (shouldClose)
        connection.Open();

    try
    {
        var columnExists = false;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(\"Users\");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "IsApproved", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "ALTER TABLE \"Users\" ADD COLUMN \"IsApproved\" INTEGER NOT NULL DEFAULT 1;";
            command.ExecuteNonQuery();
        }
    }
    finally
    {
        if (shouldClose)
            connection.Close();
    }
}
