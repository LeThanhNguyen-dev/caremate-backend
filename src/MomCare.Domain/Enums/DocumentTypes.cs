namespace MomCare.Enums;

public static class DocumentTypes
{
    public const string IdCardFront = "id_card_front";
    public const string IdCardBack = "id_card_back";
    public const string Certificate = "certificate";

    public static readonly string[] AllTypes = [IdCardFront, IdCardBack, Certificate];

    public static bool IsValid(string type) =>
        AllTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool IsIdCard(string type) =>
        type.Equals(IdCardFront, StringComparison.OrdinalIgnoreCase) ||
        type.Equals(IdCardBack, StringComparison.OrdinalIgnoreCase);
}
