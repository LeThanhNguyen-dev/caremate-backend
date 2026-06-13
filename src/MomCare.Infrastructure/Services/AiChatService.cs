using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AiChatService : IAiChatService
{
    private const string Disclaimer = "Thông tin mang tính tham khảo, không thay thế tư vấn y tế.";
    private const int DailyLimit = 30;
    private const int MinuteLimit = 5;

    private readonly MomCareContext _context;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(MomCareContext context, IGeminiService geminiService, ILogger<AiChatService> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<CreateAiChatConversationResponse> CreateConversationAsync(int userId, CancellationToken cancellationToken)
    {
        var conversation = new AiChatConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        _context.AiChatConversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAiChatConversationResponse
        {
            ConversationId = conversation.Id,
            CreatedAt = conversation.CreatedAt
        };
    }

    public async Task<IReadOnlyList<AiChatConversationDto>> GetConversationsAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.AiChatConversations
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "active")
            .OrderByDescending(x => x.LastMessageAt ?? x.CreatedAt)
            .Select(x => new AiChatConversationDto
            {
                Id = x.Id,
                Title = x.Title,
                Status = x.Status,
                MessageCount = x.MessageCount,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.LastMessageAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<AiChatMessageDto>> SendMessageAsync(int userId, Guid conversationId, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ServiceResult<AiChatMessageDto>.Fail("Tin nhắn không được để trống.");
        }

        var conversation = await _context.AiChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId && x.Status == "active", cancellationToken);

        if (conversation is null)
        {
            return ServiceResult<AiChatMessageDto>.Fail("Không tìm thấy cuộc trò chuyện.");
        }

        return await SendMessageCoreAsync(userId, conversation, content, cancellationToken);
    }

    public async Task<ServiceResult<AiChatMessageDto>> SendOrCreateMessageAsync(int userId, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ServiceResult<AiChatMessageDto>.Fail("Tin nhắn không được để trống.");
        }

        var conversation = new AiChatConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        _context.AiChatConversations.Add(conversation);
        return await SendMessageCoreAsync(userId, conversation, content, cancellationToken);
    }

    private async Task<ServiceResult<AiChatMessageDto>> SendMessageCoreAsync(int userId, AiChatConversation conversation, string content, CancellationToken cancellationToken)
    {
        var limitError = await GetLimitErrorAsync(userId, cancellationToken);
        if (limitError is not null)
        {
            return ServiceResult<AiChatMessageDto>.Fail(limitError);
        }

        var now = DateTime.UtcNow;
        var trimmed = content.Trim();
        var userMessage = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "user",
            Content = trimmed,
            CreatedAt = now
        };
        _context.AiChatMessages.Add(userMessage);

        var safety = SafetyGuardrailEngine.EvaluateText(trimmed);
        var assistant = safety.SafetyLevel == "urgent"
            ? BuildSafetyResponse(conversation.Id, safety)
            : await BuildGeminiResponseAsync(conversation.Id, userId, trimmed, cancellationToken);

        _context.AiChatMessages.Add(assistant);
        conversation.MessageCount += 2;
        conversation.LastMessageAt = assistant.CreatedAt;
        conversation.Title ??= BuildTitle(trimmed);

        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AiChatMessageDto>.Ok(Map(assistant));
    }

    private async Task<string?> GetLimitErrorAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var dayCount = await _context.AiChatMessages
            .Where(x => x.Conversation.UserId == userId && x.Role == "user" && x.CreatedAt >= today)
            .CountAsync(cancellationToken);

        if (dayCount >= DailyLimit)
        {
            return "Bạn đã đạt giới hạn 30 tin nhắn AI trong ngày. Hãy liên hệ y tá hoặc hỗ trợ CareMate nếu cần trao đổi thêm.";
        }

        var minuteStart = now.AddMinutes(-1);
        var minuteCount = await _context.AiChatMessages
            .Where(x => x.Conversation.UserId == userId && x.Role == "user" && x.CreatedAt >= minuteStart)
            .CountAsync(cancellationToken);

        return minuteCount >= MinuteLimit
            ? "Bạn đang gửi tin nhắn quá nhanh. Vui lòng thử lại sau ít phút."
            : null;
    }

    private static AiChatMessage BuildSafetyResponse(Guid conversationId, SafetyEvaluationDto safety) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = "assistant",
        Content = safety.Notice ?? "Dấu hiệu bạn mô tả cần được đánh giá trực tiếp bởi nhân viên y tế. Hãy liên hệ bác sĩ hoặc cơ sở y tế gần nhất.",
        SafetyFlag = true,
        SafetyTriggeredBy = safety.Triggers.FirstOrDefault() ?? "rule_based",
        FallbackMode = true,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<AiChatMessage> BuildGeminiResponseAsync(Guid conversationId, int userId, string content, CancellationToken cancellationToken)
    {
        try
        {
            var recent = await _context.AiChatMessages
                .AsNoTracking()
                .Where(x => x.Conversation.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new { x.Role, x.Content })
                .ToListAsync(cancellationToken);

            var response = await _geminiService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = """
Bạn là CareMate AI, trợ lý chăm sóc sức khỏe mẹ và bé.
Chỉ cung cấp thông tin tham khảo chung, không chẩn đoán, không kê đơn, không hướng dẫn dùng thuốc cụ thể.
Nếu phát hiện dấu hiệu nguy hiểm, nhắc người dùng liên hệ bác sĩ hoặc cơ sở y tế ngay.
Trả lời tiếng Việt thân thiện, tối đa 150 từ.
""",
                Prompt = $"Lịch sử gần đây: {System.Text.Json.JsonSerializer.Serialize(recent)}\nTin nhắn mới: {content}",
                Temperature = 0.2,
                MaxOutputTokens = 350
            }, cancellationToken);

            var text = string.IsNullOrWhiteSpace(response.Text)
                ? "CareMate AI chưa thể trả lời lúc này. Bạn có thể hỏi lại ngắn gọn hơn hoặc liên hệ y tá nếu cần hỗ trợ trực tiếp."
                : response.Text.Trim();

            return new AiChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = "assistant",
                Content = AppendDisclaimer(text),
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Gemini AI chat failed.");
            return new AiChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = "assistant",
                Content = "CareMate AI đang tạm thời không phản hồi. Nếu mẹ hoặc bé có dấu hiệu bất thường, hãy liên hệ y tá/bác sĩ để được hỗ trợ trực tiếp.",
                FallbackMode = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    private static string AppendDisclaimer(string content)
    {
        return content.Contains("tham khảo", StringComparison.OrdinalIgnoreCase)
            ? content
            : $"{content}\n\n{Disclaimer}";
    }

    private static string BuildTitle(string content) =>
        content.Length <= 80 ? content : content[..80].TrimEnd() + "...";

    private static AiChatMessageDto Map(AiChatMessage message) => new()
    {
        MessageId = message.Id,
        ConversationId = message.ConversationId,
        Role = message.Role,
        Content = message.Content,
        SafetyFlag = message.SafetyFlag,
        SafetyTriggeredBy = message.SafetyTriggeredBy,
        CtaAction = message.SafetyFlag ? "contact_nurse" : null,
        Disclaimer = Disclaimer,
        FallbackMode = message.FallbackMode,
        CreatedAt = message.CreatedAt
    };
}
