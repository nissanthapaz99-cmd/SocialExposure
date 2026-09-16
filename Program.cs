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
        new DirectoryInfo(
            Path.Combine(
                builder.Environment.ContentRootPath,
                ".keys")));

// Authentication
builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnValidatePrincipal = async context =>
        {
            var idValue =
                context.Principal?.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var claimedRole =
                context.Principal?.FindFirstValue(
                    ClaimTypes.Role);

            if (!int.TryParse(idValue, out var userId))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return;
            }

            var db =
                context.HttpContext.RequestServices
                    .GetRequiredService<ApplicationDbContext>();

            var user =
                await db.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.Id == userId);

            if (user == null ||
                !user.IsActive ||
                !user.IsApproved ||
                !string.Equals(
                    user.Role,
                    claimedRole,
                    StringComparison.Ordinal))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// SQL Server database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure()));

// Services
builder.Services.AddScoped<OTPService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

// =========================================
// CREATE DATABASE / TABLES
// =========================================

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    // Create the database and any missing tables
    // when the database does not already exist.
    context.Database.EnsureCreated();

    // Add/update required User profile columns.
    // EnsureUserProfileColumns(context);

    // Add EventStaff table for multiple staff members
    // assigned to the same event.
    // EnsureEventStaffTable(context);

    if (app.Environment.IsDevelopment())
    {
        var passwordHasher =
            scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<User>>();

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

// =========================================
// HTTP REQUEST PIPELINE
// =========================================

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

// =========================================
// DEVELOPMENT USER
// =========================================

static void SeedDevelopmentUser(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    string fullName,
    string email,
    string role,
    string developmentPassword)
{
    var normalizedEmail =
        email.Trim().ToLowerInvariant();

    if (context.Users.Any(
        x => x.Email.ToLower() == normalizedEmail))
    {
        return;
    }

    var user = new User
    {
        FullName = fullName,
        Email = normalizedEmail,
        Role = role,
        CreatedAt = DateTime.UtcNow,
        IsVerified = true,
        IsActive = true,
        IsApproved = true,
        ApprovedAt = DateTime.UtcNow
    };

    user.Password =
        passwordHasher.HashPassword(
            user,
            developmentPassword);

    context.Users.Add(user);
}

// =========================================
// ENSURE USER PROFILE COLUMNS
// =========================================

static void EnsureUserProfileColumns(
    ApplicationDbContext context)
{
    var connection =
        context.Database.GetDbConnection();

    var shouldClose =
        connection.State != ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        var existingColumns =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        using (var command =
               connection.CreateCommand())
        {
            command.CommandText =
                "PRAGMA table_info(\"Users\");";

            using var reader =
                command.ExecuteReader();

            while (reader.Read())
            {
                existingColumns.Add(
                    reader.GetString(1));
            }
        }

        var requiredColumns =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["IsApproved"] =
                    "INTEGER NOT NULL DEFAULT 1",

                ["CompanyName"] =
                    "TEXT NULL",

                ["PhoneNumber"] =
                    "TEXT NULL",

                ["JobTitle"] =
                    "TEXT NULL",

                ["AccessReason"] =
                    "TEXT NULL",

                ["PreferredContactMethod"] =
                    "TEXT NULL",

                ["CreatedAt"] =
                    "TEXT NULL",

                ["TermsAcceptedAt"] =
                    "TEXT NULL",

                ["PrivacyAcceptedAt"] =
                    "TEXT NULL",

                ["ApprovedAt"] =
                    "TEXT NULL",

                ["ApprovedByUserId"] =
                    "INTEGER NULL"
            };

        foreach (var column in requiredColumns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            using var command =
                connection.CreateCommand();

            command.CommandText =
                $"ALTER TABLE \"Users\" " +
                $"ADD COLUMN \"{column.Key}\" {column.Value};";

            command.ExecuteNonQuery();
        }
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

// =========================================
// ENSURE EVENT STAFF TABLE
// =========================================

static void EnsureEventStaffTable(
    ApplicationDbContext context)
{
    var connection =
        context.Database.GetDbConnection();

    var shouldClose =
        connection.State != ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        // Create EventStaff table if it does not exist.
        using (var command =
               connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""EventStaff"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_EventStaff"" PRIMARY KEY AUTOINCREMENT,
                    ""EventId"" INTEGER NOT NULL,
                    ""StaffId"" INTEGER NOT NULL,
                    CONSTRAINT ""FK_EventStaff_Events_EventId""
                        FOREIGN KEY (""EventId"")
                        REFERENCES ""Events"" (""Id"")
                        ON DELETE CASCADE,
                    CONSTRAINT ""FK_EventStaff_Users_StaffId""
                        FOREIGN KEY (""StaffId"")
                        REFERENCES ""Users"" (""Id"")
                        ON DELETE RESTRICT
                );
            ";

            command.ExecuteNonQuery();
        }

        // Prevent the same staff member from being
        // assigned to the same event more than once.
        using (var command =
               connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS
                ""IX_EventStaff_EventId_StaffId""
                ON ""EventStaff"" (""EventId"", ""StaffId"");
            ";

            command.ExecuteNonQuery();
        }

        // Index EventId for faster event/staff lookups.
        using (var command =
               connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS
                ""IX_EventStaff_EventId""
                ON ""EventStaff"" (""EventId"");
            ";

            command.ExecuteNonQuery();
        }

        // Index StaffId for faster staff/event lookups.
        using (var command =
               connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS
                ""IX_EventStaff_StaffId""
                ON ""EventStaff"" (""StaffId"");
            ";

            command.ExecuteNonQuery();
        }
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}