namespace MomCare.Dto;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalNurses { get; set; }
    public int PendingNurseApprovals { get; set; }
    public int OpenDisputes { get; set; }
    public int PendingBookings { get; set; }
}
