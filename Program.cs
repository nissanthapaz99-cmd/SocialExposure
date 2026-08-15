using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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
    });

// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Register services used by AccountController
builder.Services.AddScoped<OTPService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

// Create database/tables if they don't exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

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
    if (context.Users.Any(x => x.Email == email))
        return;

    var user = new User
    {
        FullName = fullName,
        Email = email,
        Role = role,
        IsVerified = true,
        IsActive = true
    };

    user.Password = passwordHasher.HashPassword(user, developmentPassword);
    context.Users.Add(user);
}
