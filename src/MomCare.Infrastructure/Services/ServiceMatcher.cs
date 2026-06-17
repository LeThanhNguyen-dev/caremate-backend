using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

public class ServiceMatcher
{
    private static readonly Dictionary<string, string[]> NeedToCategoryMap = new()
    {
        ["cham_soc_vet_mo"] = ["cham-me-sau-sinh"],
        ["ho_tro_cho_bu"] = ["tu-van-tai-nha"],
        ["tu_van_tam_ly"] = ["ho-tro-tinh-than"],
        ["theo_doi_sot"] = ["cham-me-sau-sinh", "cham-be-so-sinh"],
        ["theo_doi_huyet_ap"] = ["cham-me-sau-sinh"],
        ["theo_doi_chay_mau"] = ["cham-me-sau-sinh"],
        ["ho_tro_giao_suc"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
        ["ho_tro_tieu_hoa"] = ["tu-van-tai-nha", "cham-me-sau-sinh"],
        ["ho_tro_giac_ngu_be"] = ["cham-be-so-sinh"],
        ["giam_dau"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
    };

    private static readonly Dictionary<string, string[]> TagToCategoryMap = new()
    {
        ["sinh_mo"] = ["cham-me-sau-sinh"],
        ["sinh_thuong"] = ["cham-me-sau-sinh"],
        ["sua_it"] = ["tu-van-tai-nha"],
        ["sua_du"] = ["cham-be-so-sinh"],
        ["tam_trang_tieu_cuc"] = ["ho-tro-tinh-than"],
        ["be_bu_kem"] = ["tu-van-tai-nha", "cham-be-so-sinh"],
        ["be_bu_tot"] = ["cham-be-so-sinh"],
        ["be_ngu_kem"] = ["cham-be-so-sinh"],
        ["sot_nhe"] = ["cham-me-sau-sinh"],
        ["sot_cao"] = ["cham-me-sau-sinh"],
        ["huyet_ap_cao"] = ["cham-me-sau-sinh"],
        ["huyet_ap_thap"] = ["cham-me-sau-sinh"],
        ["mat_ngu_nang"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe", "ho-tro-tinh-than"],
        ["mat_ngu_nhe"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
        ["vet_mo_bat_thuong"] = ["cham-me-sau-sinh"],
        ["trieu_chung_sot"] = ["cham-me-sau-sinh", "cham-be-so-sinh"],
        ["trieu_chung_chong_mat"] = ["cham-me-sau-sinh"],
        ["trieu_chung_day_bung"] = ["tu-van-tai-nha", "cham-me-sau-sinh"],
        ["trieu_chung_tao_bon"] = ["tu-van-tai-nha", "cham-me-sau-sinh"],
        ["dau_bung_nhe"] = ["cham-me-sau-sinh"],
        ["dau_bung_vua"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
        ["dau_bung_nhieu"] = ["cham-me-sau-sinh"],
        ["dau_vet_mo_nhe"] = ["cham-me-sau-sinh"],
        ["dau_vet_mo_vua"] = ["cham-me-sau-sinh"],
        ["dau_vet_mo_nhieu"] = ["cham-me-sau-sinh"],
        ["dau_nguc_nhe"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
        ["dau_nguc_vua"] = ["cham-me-sau-sinh", "phuc-hoi-suc-khoe"],
        ["dau_nguc_nhieu"] = ["cham-me-sau-sinh"],
    };

    public List<(string ServiceId, int Score, List<string> MatchedNeeds)> Match(
        SymptomTagResult tags,
        List<ServiceSummaryForAi> services)
    {
        var matchedNeedsByService = new Dictionary<string, HashSet<string>>();

        foreach (var need in tags.PrimaryNeeds)
        {
            if (!NeedToCategoryMap.TryGetValue(need, out var categories)) continue;
            foreach (var service in services.Where(s => categories.Any(c => s.Tags.Contains(c, StringComparer.OrdinalIgnoreCase))))
            {
                matchedNeedsByService.TryAdd(service.Id, []);
                matchedNeedsByService[service.Id].Add(need);
            }
        }

        foreach (var tag in tags.Tags)
        {
            if (!TagToCategoryMap.TryGetValue(tag, out var categories)) continue;
            foreach (var service in services.Where(s => categories.Any(c => s.Tags.Contains(c, StringComparer.OrdinalIgnoreCase))))
            {
                matchedNeedsByService.TryAdd(service.Id, []);
            }
        }

        return services.Select(s =>
        {
            var matchedNeeds = matchedNeedsByService.GetValueOrDefault(s.Id, []);
            var needsScore = matchedNeeds.Count * 25;

            var tagBonus = 0;
            foreach (var tag in tags.Tags)
            {
                if (TagToCategoryMap.TryGetValue(tag, out var cats) &&
                    cats.Any(c => s.Tags.Contains(c, StringComparer.OrdinalIgnoreCase)))
                {
                    tagBonus += 10;
                }
            }

            var stageScore = tags.PostpartumStage switch
            {
                "early" when s.Tags.Any(t => t is "cham-me-sau-sinh" or "cham-be-so-sinh") => 15,
                "mid" => 10,
                "late" when s.Tags.Contains("phuc-hoi-suc-khoe") => 10,
                _ => 5,
            };

            var baseScore = 30;
            var score = Math.Min(100, baseScore + needsScore + tagBonus + stageScore);

            return (ServiceId: s.Id, Score: score, MatchedNeeds: matchedNeeds.ToList());
        })
        .Where(x => x.Score >= 40 || x.MatchedNeeds.Count > 0)
        .OrderByDescending(x => x.Score)
        .ThenBy(x => services.First(s => s.Id == x.ServiceId).Price)
        .Take(6)
        .ToList();
    }
}
