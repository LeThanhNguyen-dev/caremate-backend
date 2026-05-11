namespace MomCare.Dto;

public class AvailabilitySlotDto
{
    public int Id { get; set; }
    public int NurseProfileId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; } // Derived field: true if no active booking exists
}
