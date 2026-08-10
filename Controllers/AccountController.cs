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

        public AccountController(
            ApplicationDbContext context,
            OTPService otpService,
            EmailService emailService)
        {
            _context = context;
            _otpService = otpService;
            _emailService = emailService;
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
                Role = "Client",
                Password = null,
                IsVerified = false,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            string otp = _otpService.GenerateOTP();

            await _otpService.SaveOTPAsync(model.Email, otp);

            await _emailService.SendOTPAsync(model.Email, otp);

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

            await _emailService.SendOTPAsync(
                model.Email,
                otp
            );

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

            return View(model);
        }

        [HttpPost]
        public IActionResult VerifyOTP(VerifyOTPViewModel model)
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

                _context.SaveChanges();
            }

            return RedirectToAction(
                "Dashboard",
                "Client"
            );
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
public IActionResult StaffLogin(string email, string password)
{
    var user = _context.Users.FirstOrDefault(
        x => x.Email == email &&
             x.Password == password &&
             (x.Role == "Staff" || x.Role == "Admin") &&
             x.IsActive
    );

    if (user == null)
    {
        ModelState.AddModelError(
            "",
            "Invalid staff/admin credentials."
        );

        return View();
    }

    // Admin goes to Admin Portal
    if (user.Role == "Admin")
    {
        return RedirectToAction(
            "Dashboard",
            "Admin"
        );
    }

    // Staff goes to Staff Portal
    if (user.Role == "Staff")
    {
        return RedirectToAction(
            "Dashboard",
            "Staff"
        );
    }

    ModelState.AddModelError(
        "",
        "Invalid user role."
    );

    return View();
}
}
}