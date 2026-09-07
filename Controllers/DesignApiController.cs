using System.ComponentModel.DataAnnotations;
using SocialExposure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;
using SocialExposure.Services;

namespace SocialExposure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DesignApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly OTPService _otpService;
        private readonly EmailService _emailService;
        private readonly NotificationService _notificationService;
        private readonly IWebHostEnvironment _environment;

        public DesignApiController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            OTPService otpService,
            EmailService emailService,
            NotificationService notificationService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _otpService = otpService;
            _emailService = emailService;
            _notificationService = notificationService;
            _environment = environment;
        }

        // GET: api/designapi/test
        [HttpGet("test")]
        public IActionResult GetTestStatus()
        {
            return Ok(new { 
                status = "Success", 
                message = "Connected to SocialExposure.db successfully!",
                timestamp = System.DateTime.UtcNow
            });
        }

        // POST: api/designapi/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new { error = "Full name, email, and password are required." });
            }

            if (!request.AcceptTerms)
            {
                return BadRequest(new
                {
                    error = "You must agree to the Terms & Conditions to sign up."
                });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (!new EmailAddressAttribute().IsValid(normalizedEmail))
                return BadRequest(new { error = "Enter a valid email address." });

            var existingUser = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedEmail);
            if (existingUser != null)
            {
                return BadRequest(new { error = "An account with this email already exists." });
            }

            var newUser = new User
            {
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                Role = UserRoles.Client,
                IsVerified = false,
                IsActive = true,
                IsApproved = false
            };

            newUser.Password = _passwordHasher.HashPassword(newUser, request.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            var otp = _otpService.GenerateOTP();
            await _otpService.SaveOTPAsync(newUser.Email, otp);
            var emailSent = await _emailService.SendOTPAsync(newUser.Email, otp);

            return Accepted(new {
                message = "Account registered. Verify the email, then wait for administrator approval.",
                email = newUser.Email,
                fullName = newUser.FullName,
                role = newUser.Role,
                requiresEmailVerification = true,
                requiresAdminApproval = true,
                developmentOtp = _environment.IsDevelopment() && !emailSent ? otp : null
            });
        }

        // POST: api/designapi/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Role == UserRoles.Client && u.Email.ToLower() == normalizedEmail);

            if (user == null || !user.IsActive || !user.IsVerified || !user.IsApproved)
            {
                return Unauthorized(new
                {
                    error = "The Client account is unavailable, unverified, or waiting for approval."
                });
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.Password ?? string.Empty, request.Password);

            if (verificationResult == PasswordVerificationResult.Success)
            {
                return Ok(new { 
                    token = "sample-jwt-token-xyz", 
                    message = "Login successful",
                    role = user.Role,
                    fullName = user.FullName
                });
            }

            return Unauthorized(new { error = "Invalid email or password." });
        }

        // POST: api/designapi/verify-otp
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return BadRequest(new { error = "Email and OTP code are required." });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(x =>
                x.Role == UserRoles.Client && x.Email.ToLower() == normalizedEmail);

            if (user == null || !user.IsActive)
                return BadRequest(new { error = "This Client account is not available." });

            if (!_otpService.VerifyOTP(normalizedEmail, request.OtpCode))
                return BadRequest(new { error = "Invalid or expired OTP code." });

            var wasVerified = user.IsVerified;
            user.IsVerified = true;

            if (!wasVerified && !user.IsApproved)
            {
                await _notificationService.QueueForUserAsync(
                    user.Id,
                    "Email verified",
                    "Your registration is waiting for administrator approval.",
                    "account");
                await _notificationService.QueueForRoleAsync(
                    UserRoles.Admin,
                    "Client approval requested",
                    $"{user.FullName} verified {user.Email} and is waiting for approval.",
                    "account",
                    Url.Action("ClientManagement", "Admin"));
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                status = "PendingApproval",
                message = "Email verified. The account is waiting for administrator approval."
            });
        }

        // POST: api/designapi/upload
        [HttpPost("upload")]
        public IActionResult UploadDesign([FromForm] string title, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded." });
            }

            return Ok(new { 
                id = 101, 
                message = $"Design '{title}' uploaded successfully.",
                fileName = file.FileName 
            });
        }

        // POST: api/designapi/comment
        [HttpPost("comment")]
        public IActionResult AddComment([FromBody] CommentRequest request)
        {
            if (request == null || request.DesignId <= 0 || string.IsNullOrWhiteSpace(request.CommentText))
            {
                return BadRequest(new { error = "Invalid comment data." });
            }

            return Ok(new { 
                message = "Comment added successfully.",
                designId = request.DesignId,
                commentText = request.CommentText 
            });
        }

        // POST: api/designapi/approve
        [HttpPost("approve")]
        public IActionResult ApproveDesign([FromBody] ApprovalRequest request)
        {
            if (request == null || request.DesignId <= 0)
            {
                return BadRequest(new { error = "Invalid design ID." });
            }

            return Ok(new { 
                message = $"Design #{request.DesignId} approved successfully by {request.ApprovedBy}.",
                isApproved = true 
            });
        }

        // GET: api/designapi/admin-users
        [HttpGet("admin-users")]
        public async Task<IActionResult> GetAdminUsers()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Unauthorized(new { error = "Access denied. Token missing." });
            }

            var adminUsers = await _context.Users
                .Where(u => u.Role == UserRoles.Admin)
                .Select(u => new { u.FullName, u.Email, u.Role })
                .ToListAsync();

            return Ok(adminUsers);
        }
    }

    // Request Models
    public class RegisterRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true")]
        public bool AcceptTerms { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class OtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[0-9]{6}$")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class CommentRequest
    {
        public int DesignId { get; set; }
        public string CommentText { get; set; } = string.Empty;
    }

    public class ApprovalRequest
    {
        public int DesignId { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
    }
}
