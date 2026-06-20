using MomCare.Dto;

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

    private static readonly Dictionary<string, string[]> NeedToIncludedKeyMap = new()
    {
        ["ho_tro_cho_bu"] = ["breastfeeding-support"],
        ["cham_soc_vet_mo"] = ["mother-health-monitoring", "postpartum-massage"],
        ["theo_doi_sot"] = ["mother-health-monitoring", "baby-health-monitoring"],
        ["theo_doi_huyet_ap"] = ["mother-health-monitoring"],
        ["giam_dau"] = ["postpartum-massage", "mother-health-monitoring"],
        ["ho_tro_giao_suc"] = ["postpartum-massage", "mother-health-monitoring", "nutrition-consultation"],
        ["tu_van_tam_ly"] = ["mental-wellness"],
        ["ho_tro_giac_ngu_be"] = ["baby-health-monitoring", "night-care"],
        ["ho_tro_tieu_hoa"] = ["nutrition-consultation", "mother-health-monitoring"],
    };

    private static readonly Dictionary<string, string[]> NameKeywordToNeeds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["thong tac tia sua"] = ["ho_tro_cho_bu"],
        ["thong tia sua"] = ["ho_tro_cho_bu"],
        ["cho bu"] = ["ho_tro_cho_bu"],
        ["tam be"] = ["cham-be-so-sinh"],
        ["massage"] = ["phuc-hoi-suc-khoe", "giam_dau"],
        ["tam ly"] = ["tu_van_tam_ly"],
        ["tinh than"] = ["tu_van_tam_ly"],
        ["tham van"] = ["tu_van_tam_ly"],
        ["suc khoe me"] = ["cham-me-sau-sinh"],
        ["phuc hoi me"] = ["cham-me-sau-sinh"],
        ["suc khoe be"] = ["cham-be-so-sinh"],
        ["phat trien be"] = ["cham-be-so-sinh"],
        ["dinh duong"] = ["ho_tro_tieu_hoa"],
        ["dem"] = ["ho_tro_giac_ngu_be"],
        ["nha"] = ["ho-tro-gia-dinh"],
        ["khan"] = ["cham-me-sau-sinh"],
        ["tam so sinh"] = ["cham-be-so-sinh"],
        ["so sinh"] = ["cham-be-so-sinh"],
        ["phuc hoi sau sinh"] = ["phuc-hoi-suc-khoe"],
        ["voc dang"] = ["phuc-hoi-suc-khoe"],
        ["tuyen sua"] = ["ho_tro_cho_bu"],
    };

    public List<(string ServiceId, int Score, List<string> MatchedNeeds)> Match(
        SymptomTagResult tags,
        List<ServiceSummaryForAi> services)
    {
        var matchedNeedsByService = new Dictionary<string, HashSet<string>>();
        var isMilkPainIssue = tags.Tags.Any(t => t.StartsWith("sua_") || t.Contains("sua"))
            && tags.PrimaryNeeds.Contains("ho_tro_cho_bu");

        foreach (var need in tags.PrimaryNeeds)
        {
            if (NeedToCategoryMap.TryGetValue(need, out var categories))
            {
                foreach (var service in services.Where(s =>
                    s.Tags.Any(t => categories.Contains(t, StringComparer.OrdinalIgnoreCase))))
                {
                    matchedNeedsByService.TryAdd(service.Id, []);
                    matchedNeedsByService[service.Id].Add(need);
                }
            }

            if (NeedToIncludedKeyMap.TryGetValue(need, out var includedKeys))
            {
                foreach (var service in services.Where(s =>
                    s.IsPackage && s.IncludedServiceKeys.Any(k => includedKeys.Contains(k, StringComparer.OrdinalIgnoreCase))))
                {
                    matchedNeedsByService.TryAdd(service.Id, []);
                    matchedNeedsByService[service.Id].Add(need);
                }
            }
        }

        foreach (var tag in tags.Tags)
        {
            if (TagToCategoryMap.TryGetValue(tag, out var categories))
            {
                foreach (var service in services.Where(s =>
                    s.Tags.Any(t => categories.Contains(t, StringComparer.OrdinalIgnoreCase))))
                {
                    matchedNeedsByService.TryAdd(service.Id, []);
                }
            }
        }

        if (isMilkPainIssue)
        {
            foreach (var service in services.Where(s =>
                s.IncludedServiceKeys.Contains("breastfeeding-support", StringComparer.OrdinalIgnoreCase)))
            {
                matchedNeedsByService.TryAdd(service.Id, []);
                matchedNeedsByService[service.Id].Add("ho_tro_cho_bu");
            }
        }

        foreach (var service in services)
        {
            if (matchedNeedsByService.ContainsKey(service.Id)) continue;

            var normalizedName = RemoveDiacritics(service.Name).ToLowerInvariant();
            foreach (var kv in NameKeywordToNeeds)
            {
                if (normalizedName.Contains(kv.Key))
                {
                    matchedNeedsByService.TryAdd(service.Id, []);
                    foreach (var n in kv.Value) matchedNeedsByService[service.Id].Add(n);
                    break;
                }
            }
        }

        var scored = services.Select(s =>
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

            if (s.IsPackage && s.IncludedServiceKeys.Count > 0)
            {
                var needs = tags.PrimaryNeeds;
                var keyNeeds = NeedToIncludedKeyMap
                    .Where(kv => needs.Contains(kv.Key) && kv.Value.Any(k => s.IncludedServiceKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
                    .Select(kv => kv.Key)
                    .Count();
                tagBonus += keyNeeds * 15;
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

        // Deduplicate: prefer single services over packages when similar, keep both at different scores
        .ToList();

        var topSingle = scored.Where(x => !services.First(s => s.Id == x.ServiceId).IsPackage).ToList();
        var topPackage = scored.Where(x => services.First(s => s.Id == x.ServiceId).IsPackage).ToList();
        var topNurses = topSingle.Concat(topPackage).DistinctBy(x => x.ServiceId).Take(6).ToList();

        if (topNurses.Count < 6)
        {
            var taken = new HashSet<string>(topNurses.Select(x => x.ServiceId));
            var seed = tags.Tags.Aggregate(0, (acc, t) => acc + t.GetHashCode(StringComparison.Ordinal));
            var rng = new Random(seed);
            var fillers = services
                .Where(s => !taken.Contains(s.Id) && !s.IsPackage)
                .OrderBy(_ => rng.Next())
                .Take(6 - topNurses.Count)
                .Select(s =>
                {
                    var needs = matchedNeedsByService.GetValueOrDefault(s.Id, []);
                    return (ServiceId: s.Id, Score: 30, MatchedNeeds: needs.ToList());
                });
            topNurses.AddRange(fillers);
        }

        return topNurses;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var formD = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder();
        foreach (var ch in formD)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
