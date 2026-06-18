using System.Text;
using System.Text.Json;
using MomCare.Dto;

namespace MomCare.Services;

/// <summary>
/// Builds versioned prompts for structured care plan reasoning.
/// </summary>
public class GeminiPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string BuildReasoningPrompt(SymptomTagResult tags, List<ServiceSummaryForAi> services, BookingContextForAi? booking)
    {
        return booking is null
            ? BuildRecommendationPrompt(tags, services)
            : BuildBookingPrompt(tags, services, booking);
    }

    private static string BuildRecommendationPrompt(SymptomTagResult tags, List<ServiceSummaryForAi> services)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ban la chuyen gia tu van cham soc sau sinh cua CareMate.");
        builder.AppendLine("KHONG chan doan benh. KHONG ke don thuoc. Chi goi y dich vu cham soc.");
        builder.AppendLine();
        builder.AppendLine("## Thong tin khach hang");
        builder.AppendLine($"- Giai doan hau san: {tags.PostpartumStage}");
        builder.AppendLine($"- Kieu sinh: {tags.DeliveryType}");
        builder.AppendLine($"- Tags trieu chung: {string.Join(", ", tags.Tags)}");
        builder.AppendLine($"- Nhu cau chinh: {string.Join(", ", tags.PrimaryNeeds)}");
        builder.AppendLine($"- Van de uu tien nhat: {tags.PrimaryConcern}");
        builder.AppendLine($"- Tu khoa boi canh: {string.Join(", ", tags.RelevantContextTokens)}");
        builder.AppendLine($"- Co van de cua be: {(tags.HasBabyConcern ? "co" : "khong")}");
        builder.AppendLine($"- Co van de cho bu/tac sua: {(tags.HasBreastfeedingConcern ? "co" : "khong")}");
        if (!string.IsNullOrWhiteSpace(tags.RawCheckinSummary))
        {
            builder.AppendLine();
            builder.AppendLine("## Du lieu tu form khach dien (BAT BUOC dung cac so lieu nay trong reason)");
            builder.AppendLine(tags.RawCheckinSummary);
        }
        builder.AppendLine();
        builder.AppendLine("## Dich vu kha dung");
        builder.AppendLine(JsonSerializer.Serialize(services, JsonOptions));
        builder.AppendLine();
        builder.AppendLine("## QUY TAC BAT BUOC");
        builder.AppendLine("1. Phan tich tinh trang khach dua tren CA TAGS LAN DU LIEU GOC.");

        builder.AppendLine("2. CHON 4 dich vu phu hop nhat. Dich vu 1 la QUAN TRONG NHAT (van de chinh).");
        builder.AppendLine("3. MOI reason BAT BUOC co CON SO CU THE tu du lieu goc, vi du:");
        builder.AppendLine("   - 'dau {painLevel}/10 o {painLocation}' (vd: dau 7/10 o vet mo)");
        builder.AppendLine("   - 'sot {temperature} do C' (vd: sot 38.5 do C)");
        builder.AppendLine("   - 'huyet ap {systolic}/{diastolic}' (vd: huyet ap 150/95)");
        builder.AppendLine("   - 'ngu {sleepHours}h/ngay' (vd: ngu 3h/ngay)");
        builder.AppendLine("   - 'tam trang {mood}' (vd: tam trang lo au, met moi)");
        builder.AppendLine("   - 'sua {milkStatus}' (vd: sua it, tac sua)");
        builder.AppendLine("   - 'be {babyFeeding}, be {babySleep}' (vd: be bu kem, be ngu khong ngon)");
        builder.AppendLine("4. KHONG viet reason chung chung neu khong co so lieu tu form.");
        builder.AppendLine("5. KHONG dung snake_case, under_score, ma noi bo, tieng Anh.");
        builder.AppendLine("6. KHONG viet: 'phu hop voi tinh trang cua ban', 'dich vu nay giup ban', 'goi y cham soc phu hop'.");
        builder.AppendLine("7. Neu be khong co van de -> KHONG chon dich vu cham be.");
        builder.AppendLine();
        builder.AppendLine("## Cach doi chieu dich vu:");
        builder.AppendLine("- name: Ten dich vu co tu khoa lien quan?");
        builder.AppendLine("- shortDescription: Mo ta co nhac toi trieu chung cua khach?");
        builder.AppendLine("- tags: Co tag trung voi primaryConcern hoac tags cua khach?");
        builder.AppendLine("- includedServiceKeys: Co key nhu breastfeeding-support, wound-care, mental-health...?");
        builder.AppendLine();
        builder.AppendLine("## Vidu reason DUNG (BAT BUOC co CON SO):");
        builder.AppendLine("- 'Khach dau 7/10 o vet mo keo dai 3 ngay, vet mo sung do. Dich vu cham soc vet mo co tag cham-soc-vet-mo, giam sat va xu ly vet thuong hang ngay.'");
        builder.AppendLine("- 'Khach bi tac sua, sua it, dau nguc. Nhiet do 38.5°C. Dich vu ho tro cho bu co key breastfeeding-support, huong dan me cach cho bu dung tu the va thong tac sua.'");
        builder.AppendLine("- 'Khach mat ngu chi ngu 3h/ngay, tam tram lo au. Dich vu ho tro tinh than co tag ho-tro-tinh-than, giup me on dinh tam ly va cai thien giac ngu.'");
        builder.AppendLine();
        builder.AppendLine("## Vidu reason SAI (tuyet doi tranh):");
        builder.AppendLine("- 'Phu hop voi tinh trang cua ban.'");
        builder.AppendLine("- 'Dich vu nay co the ho tro ban trong qua trinh phuc hoi.'");
        builder.AppendLine("- 'Goi y cham soc phu hop cho me sau sinh.'");
        builder.AppendLine();
        builder.AppendLine("## Yeu cau reasoning");
        builder.AppendLine("Viet 2-3 cau tieng Viet tu nhien, BAT BUOC nhac toi CON SO CU THE cua khach, giai thich tai sao 4 dich vu nay duoc chon.");
        builder.AppendLine();
        builder.AppendLine("## Dinh dang JSON (CHI 2 truong: serviceScores va reasoning)");
        builder.AppendLine("""
{
  "serviceScores": [
    {
      "serviceId": "12",
      "score": 0.86,
      "reason": "Khach dau 7/10 o vet mo, vet mo sung do, co nguy co nhiem trung. Dich vu cham soc vet mo co tag cham-soc-vet-mo, giam sat vet mo hang ngay, giup me tranh bien chung."
    },
    {
      "serviceId": "7",
      "score": 0.71,
      "reason": "Khach bi tac sua, dau nguc, sua it. Nhiet do 38.5°C can theo doi. Dich vu ho tro cho bu co key breastfeeding-support, huong dan cho bu dung tu the va giam dau."
    },
    {
      "serviceId": "15",
      "score": 0.56,
      "reason": "Khach can phuc hoi the chat sau sinh mo. Dich vu phuc hoi suc khoe co tag phuc-hoi-suc-khoe, giup me tap vandong nhe va an uong khoa hoc."
    },
    {
      "serviceId": "9",
      "score": 0.48,
      "reason": "Khach ngu 3h/ngay, tam trang lo au, met moi. Dich vu ho tro tinh than co tag ho-tro-tinh-than, giup me thu gian va on dinh tam ly."
    }
  ],
  "reasoning": "Khach dau 7/10 o vet mo sau sinh mo, vet mo sung do can cham soc vet mo. Co tinh trang tac sua, sua it va nhiet do 38.5°C can ho tro cho bu va theo doi sot. Them phuc hoi suc khoe va ho tro tinh than vi me met moi, mat ngu. Be khong co van de nen khong can dich vu cham be."
}
""");

        return builder.ToString();
    }

    private static string BuildBookingPrompt(SymptomTagResult tags, List<ServiceSummaryForAi> services, BookingContextForAi booking)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ban la he thong lap lo trinh cham soc sau sinh cua CareMate.");
        builder.AppendLine("KHONG chan doan benh. KHONG ke don thuoc. Chi de xuat noi dung cham soc an toan.");
        builder.AppendLine();
        builder.AppendLine("## Thong tin khach hang");
        builder.AppendLine($"- Giai doan hau san: {tags.PostpartumStage}");
        builder.AppendLine($"- Kieu sinh: {tags.DeliveryType}");
        builder.AppendLine($"- Tags trieu chung: {string.Join(", ", tags.Tags)}");
        builder.AppendLine($"- Nhu cau chinh: {string.Join(", ", tags.PrimaryNeeds)}");
        builder.AppendLine($"- Van de uu tien nhat: {tags.PrimaryConcern}");
        if (!string.IsNullOrWhiteSpace(tags.RawCheckinSummary))
        {
            builder.AppendLine();
            builder.AppendLine("## Du lieu tu form khach dien");
            builder.AppendLine(tags.RawCheckinSummary);
        }
        builder.AppendLine();
        builder.AppendLine("## Dich vu kha dung");
        builder.AppendLine(JsonSerializer.Serialize(services, JsonOptions));
        builder.AppendLine();
        builder.AppendLine("## Booking hien tai");
        builder.AppendLine($"- Goi: {booking.ServiceName}");
        builder.AppendLine($"- Con {booking.RemainingSessionCount} buoi");
        builder.AppendLine($"- Buoi tiep theo: {booking.NextSessionDate:dd/MM/yyyy}");
        builder.AppendLine();
        builder.AppendLine("## Yeu cau");
        builder.AppendLine("Tra ve JSON hop le theo schema sau. KHONG markdown. KHONG text ngoai JSON.");
        builder.AppendLine("""
{
  "serviceScores": [
    {
      "serviceId": "1",
      "score": 0.85,
      "reason": "Ly do cu the 1-2 cau tieng Viet tu nhien theo trieu chung cua khach",
      "matchedNeeds": ["need_tag"]
    }
  ],
  "planItems": [
    {
      "sessionNumber": 1,
      "suggestedDate": "D+1",
      "focus": "Tieu de buoi",
      "activities": ["Hoat dong 1", "Hoat dong 2"],
      "note": "Luu y",
      "estimatedDurationMinutes": 90
    }
  ],
  "reasoning": "Tom tat 2-3 cau tai sao plan nay phu hop"
}
""");

        return builder.ToString();
    }
}
