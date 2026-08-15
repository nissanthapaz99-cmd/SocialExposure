namespace SocialExposure.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Client = "Client";

    public static bool IsValid(string? role) =>
        role is Admin or Staff or Client;
}
