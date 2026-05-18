using System.ComponentModel.DataAnnotations;

namespace MomCare.Dto;

public class CreateBookingDto
{
    [Required]
    public int NurseId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    // Optional for package bookings (multi-day packages don't require a specific slot)
    public int? AvailabilitySlotId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    // Optional for package bookings (auto-calculated from PackageDays)
    public DateTime? EndTime { get; set; }

    // Optional for package bookings. When provided, each item is the concrete start time
    // for one package session, ordered by session number.
    public List<DateTime>? PackageSessionStartTimes { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

