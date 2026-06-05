using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IPackageSessionService
{
    Task<ServiceResult<PackageProgressDto>> GetProgressAsync(int actorUserId, int bookingId);
    Task<ServiceResult<PackageSessionDto>> CheckInAsync(int nurseUserId, int bookingId, CheckInSessionDto dto);
    Task<ServiceResult<PackageSessionDto>> CheckOutAsync(int nurseUserId, int bookingId, CheckOutSessionDto dto);
    Task<ServiceResult<PackageSessionDto>> SubmitPackageSessionFeedbackAsync(int customerUserId, int bookingId, int sessionId, CustomerSessionFeedbackDto dto);
    Task<ServiceResult<BookingDetailDto>> SubmitSingleSessionFeedbackAsync(int customerUserId, int bookingId, CustomerSessionFeedbackDto dto);
}
