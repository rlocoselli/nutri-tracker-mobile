namespace NutritionTracker.Models;

public class FriendInvite
{
    public string Email { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
