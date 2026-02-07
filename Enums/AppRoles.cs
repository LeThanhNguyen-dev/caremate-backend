namespace MomCare.Enums;

public static class AppRoles
{
    public const string Customer = "customer";
    public const string Nurse = "nurse"; // Kept for backward compatibility if needed, but transitioning to below
    public const string NurseUnconfirmed = "nurse_unconfirmed";
    public const string NurseConfirmed = "nurse_confirmed";
    public const string Admin = "admin";
}
