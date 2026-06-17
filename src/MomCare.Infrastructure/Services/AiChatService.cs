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
    private const string OutOfScopeReply = "Mình chỉ hỗ trợ câu hỏi y tế tham khảo và hướng dẫn liên quan đến CareMate. Nếu mẹ muốn, mẹ có thể hỏi về triệu chứng, chăm sóc sức khỏe, hoặc cách sử dụng dịch vụ CareMate.";

    private readonly MomCareContext _context;
    private readonly ILlmService _llmService;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(MomCareContext context, ILlmService llmService, ILogger<AiChatService> logger)
    {
        _context = context;
        _llmService = llmService;
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

        var conversation = await _context.AiChatConversations
            .Where(x => x.UserId == userId && x.Status == "active")
            .OrderByDescending(x => x.LastMessageAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            conversation = new AiChatConversation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
            _context.AiChatConversations.Add(conversation);
        }

        return await SendMessageCoreAsync(userId, conversation, content, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<AiChatMessageDto>>> GetMessagesAsync(int userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversationExists = await _context.AiChatConversations
            .AnyAsync(x => x.Id == conversationId && x.UserId == userId, cancellationToken);

        if (!conversationExists)
        {
            return ServiceResult<IReadOnlyList<AiChatMessageDto>>.Fail("Không tìm thấy cuộc trò chuyện.");
        }

        var messages = await _context.AiChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<AiChatMessageDto>>.Ok(messages.Select(Map).ToList());
    }

    private async Task<ServiceResult<AiChatMessageDto>> SendMessageCoreAsync(int userId, AiChatConversation conversation, string content, CancellationToken cancellationToken)
    {
        var limitError = await GetLimitErrorAsync(userId, cancellationToken);
        if (limitError is not null)
        {
            return ServiceResult<AiChatMessageDto>.Fail(limitError);
        }

        var trimmed = content.Trim();
        var now = DateTime.UtcNow;

        _context.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "user",
            Content = trimmed,
            CreatedAt = now
        });

        var safety = SafetyGuardrailEngine.EvaluateText(trimmed);
        var assistant = safety.SafetyLevel == "urgent"
            ? BuildSafetyResponse(conversation.Id, safety)
            : await BuildLlmResponseAsync(conversation.Id, trimmed, cancellationToken);

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
        Content = safety.Notice ?? "Dấu hiệu mẹ mô tả cần được đánh giá trực tiếp bởi nhân viên y tế. Hãy liên hệ bác sĩ hoặc cơ sở y tế gần nhất.",
        SafetyFlag = true,
        SafetyTriggeredBy = safety.Triggers.FirstOrDefault() ?? "rule_based",
        FallbackMode = true,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<AiChatMessage> BuildLlmResponseAsync(Guid conversationId, string content, CancellationToken cancellationToken)
    {
        try
        {
            var recent = await _context.AiChatMessages
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            var contents = new List<GeminiContentDto>();
            foreach (var msg in recent)
            {
                contents.Add(new GeminiContentDto
                {
                    Role = msg.Role == "assistant" ? "model" : "user",
                    Parts = [new GeminiPartDto { Text = msg.Content }]
                });
            }

            contents.Add(new GeminiContentDto
            {
                Role = "user",
                Parts = [new GeminiPartDto { Text = content }]
            });

            var response = await _llmService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = BuildCareMateSystemInstruction(),
                Contents = contents,
                Prompt = content,
                Temperature = 0.2,
                MaxOutputTokens = 320,
                TimeoutSeconds = 20
            }, cancellationToken);

            var text = NormalizeAssistantText(response.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "CareMate AI chưa thể trả lời lúc này. Mẹ có thể hỏi lại ngắn gọn hơn hoặc liên hệ y tá nếu cần hỗ trợ trực tiếp.";
            }

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
            _logger.LogWarning(ex, "CareMate AI chat failed.");
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

    private static string BuildCareMateSystemInstruction() =>
        """
Bạn là CareMate AI trong dự án CareMate.

Mục tiêu:
- Trả lời các câu hỏi y tế tham khảo và các câu hỏi liên quan đến CareMate.
- Có thể hỗ trợ:
1. các câu hỏi y tế tham khảo nói chung,
2. chăm sóc mẹ sau sinh,
3. chăm sóc bé sơ sinh,
4. tâm lý sau sinh,
5. hướng dẫn sử dụng dịch vụ hoặc tính năng CareMate.

Quy tắc bắt buộc:
1. Chỉ trả lời bằng tiếng Việt có dấu, tự nhiên, dễ đọc.
2. Xưng hô là "mẹ".
3. Ưu tiên trả lời bằng 1 đoạn văn ngắn 2 đến 4 câu, mạch lạc, ấm áp, dễ hiểu.
4. Chỉ dùng gạch đầu dòng khi thật sự cần liệt kê vài ý rõ ràng.
5. Không tạo bullet rỗng, không tạo dòng chỉ có dấu "-", không xuống dòng thừa.
6. Không kết thúc bằng câu dang dở. Nếu cần ngắn lại, hãy dừng ở một câu hoàn chỉnh.
7. Không chẩn đoán bệnh.
8. Không kê thuốc, không hướng dẫn liều dùng, không đưa phác đồ điều trị.
9. Nếu câu hỏi không liên quan đến y tế, sức khỏe, triệu chứng, chăm sóc, hoặc CareMate, không trả lời nội dung câu hỏi.
10. Với câu hỏi ngoài phạm vi, chỉ trả lời đúng câu này:
"Mình chỉ hỗ trợ câu hỏi y tế tham khảo và hướng dẫn liên quan đến CareMate. Nếu mẹ muốn, mẹ có thể hỏi về triệu chứng, chăm sóc sức khỏe, hoặc cách sử dụng dịch vụ CareMate."
11. Nếu có dấu hiệu nguy hiểm, khuyên mẹ liên hệ bác sĩ hoặc cơ sở y tế ngay.
12. Không để lộ suy luận nội bộ. Không viết các câu như "We need to respond", "Reasoning", "Phân tích".
13. Chỉ xuất ra câu trả lời cuối cùng cho người dùng.
""";

    private static string NormalizeAssistantText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var text = content.Trim();
        var leakageMarkers = new[]
        {
            "We need to respond",
            "Reasoning:",
            "The user says:",
            "According to rules",
            "They want",
            "We should respond",
            "Thus produce"
        };

        if (leakageMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            var paragraphs = text
                .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Reverse();

            foreach (var paragraph in paragraphs)
            {
                if (!leakageMarkers.Any(marker => paragraph.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    text = paragraph.Trim();
                    break;
                }
            }
        }

        text = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        text = text.Replace("\n-\n", "\n", StringComparison.Ordinal);
        text = text.Replace("\n- \n", "\n", StringComparison.Ordinal);
        text = text.Replace(":\n- ", ": ", StringComparison.Ordinal);
        text = text.Replace(":\n", ": ", StringComparison.Ordinal);

        while (text.Contains("\n\n\n", StringComparison.Ordinal))
        {
            text = text.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        if (text.EndsWith("-", StringComparison.Ordinal))
        {
            text = text[..^1].TrimEnd();
        }

        return text.Trim();
    }

    private static string AppendDisclaimer(string content)
    {
        if (string.Equals(content.Trim(), OutOfScopeReply, StringComparison.Ordinal))
        {
            return content.Trim();
        }

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
