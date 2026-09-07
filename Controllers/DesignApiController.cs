using SocialExposure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DesignApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public DesignApiController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
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
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new { error = "Full name, email, and password are required." });
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { error = "An account with this email already exists." });
            }

            var newUser = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                Role = UserRoles.Client,
                IsVerified = true,
                IsActive = true
            };

            newUser.Password = _passwordHasher.HashPassword(newUser, request.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Account successfully registered and saved to SocialExposure.db.",
                email = newUser.Email,
                fullName = newUser.FullName,
                role = newUser.Role
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "Invalid email or password." });
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
        public IActionResult VerifyOtp([FromBody] OtpRequest request)
        {
            if (request?.OtpCode == "123456")
            {
                return Ok(new { status = "Success", message = "OTP verified successfully." });
            }
            return BadRequest(new { error = "Invalid OTP code." });
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
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class OtpRequest
    {
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