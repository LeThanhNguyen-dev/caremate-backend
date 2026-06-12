using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IBookingService
{
    Task<ServiceResult<BookingDetailDto>> CreateBookingAsync(int customerId, CreateBookingDto dto);
    Task<IEnumerable<BookingDetailDto>> GetCustomerBookingsAsync(int customerId);
    Task<IEnumerable<BookingDetailDto>> GetNurseBookingsAsync(int nurseId);
    Task<BookingDetailDto?> GetBookingDetailAsync(int actorUserId, int bookingId, bool isAdmin);
    Task<IEnumerable<BookingStatusHistoryDto>?> GetBookingHistoryAsync(int actorUserId, int bookingId, bool isAdmin);
    Task<ServiceResult<bool>> UpdateBookingStatusAsync(int actorUserId, bool isAdmin, UpdateBookingStatusDto dto, int bookingId);
    Task<ServiceResult<bool>> CancelBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancelBookingDto dto);
}
