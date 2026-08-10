using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Services
{
    public class OTPService
    {
        private readonly ApplicationDbContext _context;

        public OTPService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Generate a random 6-digit OTP
        public string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Save OTP to the database
        public async Task SaveOTPAsync(string email, string code)
        {
            var otp = new OTP
            {
                Email = email,
                Code = code,
                ExpiryTime = DateTime.Now.AddMinutes(10),
                IsUsed = false
            };

            _context.OTPs.Add(otp);
            await _context.SaveChangesAsync();
        }

        // Verify OTP
        public bool VerifyOTP(string email, string code)
        {
            var otp = _context.OTPs
                .OrderByDescending(x => x.Id)
                .FirstOrDefault(x =>
                    x.Email == email &&
                    x.Code == code &&
                    !x.IsUsed &&
                    x.ExpiryTime > DateTime.Now);

            if (otp == null)
                return false;

            otp.IsUsed = true;
            _context.SaveChanges();

            return true;
        }
    }
}