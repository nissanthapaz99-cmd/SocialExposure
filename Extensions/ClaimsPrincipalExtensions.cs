using System.Security.Claims;

namespace SocialExposure.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId))
            throw new InvalidOperationException("The signed-in user does not have a valid ID claim.");

        return userId;
    }
}
