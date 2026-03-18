using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IBookingService
{
    Task<BookingDetailDto?> CreateBookingAsync(int customerId, CreateBookingDto dto);
    Task<IEnumerable<BookingDetailDto>> GetCustomerBookingsAsync(int customerId);
    Task<IEnumerable<BookingDetailDto>> GetNurseBookingsAsync(int nurseId);
    Task<BookingDetailDto?> GetBookingDetailAsync(int actorUserId, int bookingId, bool isAdmin);
    Task<bool> UpdateBookingStatusAsync(int actorUserId, bool isAdmin, UpdateBookingStatusDto dto, int bookingId);
}
