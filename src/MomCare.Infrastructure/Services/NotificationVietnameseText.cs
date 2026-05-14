namespace MomCare.Services;

internal static class NotificationVietnameseText
{
    public static string BookingStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "pending_confirm" => "đang chờ xác nhận",
        "confirmed" => "đã xác nhận",
        "rejected" => "đã từ chối",
        "in_progress" => "đang thực hiện",
        "completed" => "đã hoàn thành",
        "cancelled" => "đã hủy",
        _ => status ?? "không xác định"
    };

    public static string DisputeStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "open" => "đang mở",
        "in_progress" => "đang xử lý",
        "resolved" => "đã giải quyết",
        "closed" => "đã đóng",
        "rejected" => "đã từ chối",
        _ => status ?? "không xác định"
    };

    public static string PaymentStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "pending" => "đang chờ xử lý",
        "paid" => "đã thanh toán",
        "completed" => "đã hoàn tất",
        "failed" => "thanh toán thất bại",
        "cancelled" => "đã hủy",
        "refunded" => "đã hoàn tiền",
        _ => status ?? "không xác định"
    };
}
