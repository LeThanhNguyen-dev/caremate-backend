namespace MomCare.Dto;

public class NurseServiceDto
{
    public int Id { get; set; }
    public int NurseProfileId { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = "fixed"; // fixed or hourly
    public string Status { get; set; } = "enabled";
    public DateTime CreatedAt { get; set; }
}

public class CreateNurseServiceDto
{
    public int ServiceId { get; set; }
    public decimal Price { get; set; }
    public string Unit { get; set; } = "fixed"; // fixed or hourly
}

public class UpdateNurseServiceDto
{
    public decimal Price { get; set; }
    public string Unit { get; set; } = "fixed";
}
