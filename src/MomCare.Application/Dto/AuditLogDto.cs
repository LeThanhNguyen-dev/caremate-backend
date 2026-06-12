namespace MomCare.Dto;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
