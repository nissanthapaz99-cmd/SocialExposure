using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Data;
using SocialExposure.Models;
using SocialExposure.Services;
using SocialExposure.ViewModels;

namespace SocialExposure.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OTPService _otpService;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(
            ApplicationDbContext context,
            OTPService otpService,
            EmailService emailService,
            IWebHostEnvironment environment,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _otpService = otpService;
            _emailService = emailService;
            _environment = environment;
            _passwordHasher = passwordHasher;
        }

        // ==========================
        // CLIENT REGISTER
        // ==========================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existingUser = _context.Users
                .FirstOrDefault(x => x.Email == model.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email already exists.");
                return View(model);
            }

            User user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Role = UserRoles.Client,
                Password = null,
                IsVerified = false,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            string otp = _otpService.GenerateOTP();

            await _otpService.SaveOTPAsync(model.Email, otp);

            var emailSent = await _emailService.SendOTPAsync(model.Email, otp);
            SetDevelopmentOtp(otp, emailSent);

            return RedirectToAction(
                "VerifyOTP",
                new { email = model.Email }
            );
        }


        // ==========================
        // CLIENT LOGIN
        // ==========================

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users
                .FirstOrDefault(x => x.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "Email not found."
                );

                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(
                    "",
                    "This account is inactive."
                );

                return View(model);
            }

            string otp = _otpService.GenerateOTP();

            await _otpService.SaveOTPAsync(
                model.Email,
                otp
            );

            var emailSent = await _emailService.SendOTPAsync(
                model.Email,
                otp
            );
            SetDevelopmentOtp(otp, emailSent);

            return RedirectToAction(
                "VerifyOTP",
                new { email = model.Email }
            );
        }


        // ==========================
        // VERIFY OTP
        // ==========================

        [HttpGet]
        public IActionResult VerifyOTP(string email)
        {
            VerifyOTPViewModel model = new VerifyOTPViewModel();

            model.Email = email;
            ViewBag.DevelopmentOtp = TempData["DevelopmentOtp"];

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            bool success = _otpService.VerifyOTP(
                model.Email,
                model.OTP
            );

            if (!success)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid or expired OTP."
                );

                return View(model);
            }

            var user = _context.Users
                .FirstOrDefault(x => x.Email == model.Email);

            if (user != null)
            {
                user.IsVerified = true;
                await _context.SaveChangesAsync();
                await SignInUserAsync(user);
            }

            return RedirectToRoleDashboard(user);
        }


        // ==========================
        // STAFF / ADMIN LOGIN
        // ==========================

        [HttpGet]
        public IActionResult StaffLogin()
        {
            return View();
        }
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> StaffLogin(string email, string password)
{
    var user = _context.Users.FirstOrDefault(
        x => x.Email == email &&
             (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin) &&
             x.IsActive);

    if (user == null || !VerifyStaffPassword(user, password))
    {
        ModelState.AddModelError(
            "",
            "Invalid staff/admin credentials."
        );

        return View();
    }

    await SignInUserAsync(user);
    return RedirectToRoleDashboard(user);
}

[Authorize]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction("Index", "Home");
}

[HttpGet]
public IActionResult AccessDenied()
{
    Response.StatusCode = StatusCodes.Status403Forbidden;
    return View();
}

[Authorize]
[HttpGet]
public async Task<IActionResult> Profile()
{
    var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId))
        return RedirectToAction(nameof(Login));

    var user = await _context.Users.FindAsync(userId);
    return user == null ? RedirectToAction(nameof(Login)) : View(user);
}

private async Task SignInUserAsync(User user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.FullName),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Role, user.Role)
    };

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        });
}

private IActionResult RedirectToRoleDashboard(User? user)
{
    if (user == null || !UserRoles.IsValid(user.Role))
        return RedirectToAction(nameof(Login));

    return user.Role switch
    {
        UserRoles.Admin => RedirectToAction("Dashboard", "Admin"),
        UserRoles.Staff => RedirectToAction("Dashboard", "Staff"),
        _ => RedirectToAction("Dashboard", "Client")
    };
}

private void SetDevelopmentOtp(string otp, bool emailSent)
{
    if (_environment.IsDevelopment() && !emailSent)
        TempData["DevelopmentOtp"] = otp;
}

private bool VerifyStaffPassword(User user, string suppliedPassword)
{
    if (string.IsNullOrWhiteSpace(user.Password) ||
        string.IsNullOrWhiteSpace(suppliedPassword))
        return false;

    var result = _passwordHasher.VerifyHashedPassword(
        user,
        user.Password,
        suppliedPassword);

    if (result != PasswordVerificationResult.Failed)
    {
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.Password = _passwordHasher.HashPassword(user, suppliedPassword);
            _context.SaveChanges();
        }

        return true;
    }

    // Upgrade any existing legacy plain-text staff password after a valid login.
    if (user.Password == suppliedPassword)
    {
        user.Password = _passwordHasher.HashPassword(user, suppliedPassword);
        _context.SaveChanges();
        return true;
    }

    return false;
}
}
}
