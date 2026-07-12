using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MomCare.Enums;
using MomCare.Models;

namespace MomCare.Data;

public static class MomCareSeedData
{
    private const string DefaultPassword = "MomCare@123";
    private const string AdminPassword = "gig";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<MomCareContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("MomCareSeedData");

        await context.Database.MigrateAsync();

        await EnsureRolesAsync(roleManager);

        var admin = await EnsureUserAsync(userManager, "admin@momcare.local", "System Admin", "0900000001", [AppRoles.Admin], AdminPassword, resetPassword: true);
        var customerA = await EnsureUserAsync(userManager, "lan.customer@momcare.local", "Lan Nguyen", "0900000002", [AppRoles.Customer]);
        var customerB = await EnsureUserAsync(userManager, "thu.customer@momcare.local", "Thu Tran", "0900000003", [AppRoles.Customer]);

        var nurseA = await EnsureUserAsync(userManager, "huong.nurse@momcare.local", "Lê Thị Hương", "0900000004", [AppRoles.NurseConfirmed]);
        var nurseB = await EnsureUserAsync(userManager, "mai.nurse@momcare.local", "Phạm Thanh Mai", "0900000005", [AppRoles.NurseConfirmed]);
        var nursePending = await EnsureUserAsync(userManager, "pending.nurse@momcare.local", "Đỗ Bảo Ngọc", "0900000006", [AppRoles.NurseUnconfirmed]);

        await context.SaveChangesAsync();

        await EnsureAddressAsync(context, customerA.Id, "25 Nguyễn Văn Linh, quận Hải Châu, Đà Nẵng", "Bình Hiên", "Hải Châu", true, "customer_home", 16.0605, 108.2210);
        await EnsureAddressAsync(context, customerB.Id, "120 Hồ Nghinh, quận Sơn Trà, Đà Nẵng", "Phước Mỹ", "Sơn Trà", true, "customer_home", 16.0686, 108.2431);
        await EnsureAddressAsync(context, nurseA.Id, "Chợ Hàn Đà Nẵng, 116 Bạch Đằng, Hải Châu 1, Hải Châu, Đà Nẵng", "Hải Châu 1", "Hải Châu", true, "nurse_base", 16.068244159000074, 108.22487182000003);
        await EnsureAddressAsync(context, nurseB.Id, "Hồ Nghinh, Phước Mỹ, Sơn Trà, Đà Nẵng", "Phước Mỹ", "Sơn Trà", true, "nurse_base", 16.06676487624661, 108.24317303171603);

        var babyBath = await EnsureServiceAsync(context, "Dịch vụ tắm bé & vệ sinh sơ sinh tại nhà", "Điều dưỡng hướng dẫn kỹ thuật tắm bé an toàn — tắm nước ấm, vệ sinh rốn, mắt, tai và chăm sóc da cho trẻ sơ sinh ngay tại nhà.", "cham-be-so-sinh", 350_000m, 45, "active", "single", nameEn: "Newborn Bathing & Hygiene Service at Home", descriptionEn: "Nurse guides safe baby bathing techniques — warm water bath, umbilical cord, eye, ear hygiene and skin care for newborns at home.");
        var motherHealth = await EnsureServiceAsync(context, "Theo dõi phục hồi sức khỏe mẹ sau sinh", "Kiểm tra các dấu hiệu hồi phục sau sinh: vết mổ, sản dịch, co hồi tử cung, huyết áp và hỗ trợ mẹ nhận biết sớm dấu hiệu bất thường.", "cham-me-sau-sinh", 500_000m, 60, "active", "single", nameEn: "Postpartum Maternal Health Recovery Monitoring", descriptionEn: "Check postpartum recovery signs: incision wound, lochia, uterine involution, blood pressure and help mother recognize early warning signs.");
        var babyHealth = await EnsureServiceAsync(context, "Theo dõi phát triển thể chất & phản xạ sơ sinh", "Đo cân nặng, chiều dài, vòng đầu, kiểm tra phản xạ nguyên thủy và các mốc phát triển quan trọng của trẻ trong giai đoạn sơ sinh.", "cham-be-so-sinh", 450_000m, 60, "active", "single", nameEn: "Newborn Physical Development & Reflex Monitoring", descriptionEn: "Measure weight, length, head circumference, check primitive reflexes and key developmental milestones during the newborn period.");
        var lactation = await EnsureServiceAsync(context, "Tư vấn & hỗ trợ cho bú và xử lý tắc tia sữa", "Hỗ trợ mẹ xử lý tắc tia sữa, đau đầu ti và các vấn đề cho con bú; hướng dẫn tư thế bú đúng, lịch bú khoa học, kích thích nguồn sữa.", "tu-van-tai-nha", 600_000m, 90, "active", "single", nameEn: "Breastfeeding Support & Blocked Duct Treatment", descriptionEn: "Help mother with blocked ducts, nipple pain and breastfeeding issues; guide correct latch positions, scientific feeding schedule, milk stimulation.");
        var massage = await EnsureServiceAsync(context, "Massage phục hồi cơ thể & giảm đau nhức sau sinh", "Liệu trình massage chuyên sâu — giảm đau vùng lưng, vai gáy, kích thích tuần hoàn máu, hỗ trợ phục hồi cơ xương khớp sau sinh.", "phuc-hoi-suc-khoe", 700_000m, 90, "active", "single", nameEn: "Postpartum Recovery Massage & Pain Relief", descriptionEn: "Deep therapeutic massage — relieve back, shoulder and neck pain, stimulate blood circulation, support postpartum musculoskeletal recovery.");
        var nutrition = await EnsureServiceAsync(context, "Tư vấn dinh dưỡng & xây dựng thực đơn sau sinh", "Cá nhân hóa thực đơn giàu dưỡng chất cho mẹ sau sinh — hỗ trợ phục hồi sức khỏe, lợi sữa và cân bằng dinh dưỡng cho con bú.", "tu-van-tai-nha", 550_000m, 60, "active", "single", nameEn: "Postpartum Nutrition Consultation & Meal Planning", descriptionEn: "Personalized nutrient-rich meal plan for new mothers — supports health recovery, milk production and balanced nutrition for breastfeeding.");
        var nightCare = await EnsureServiceAsync(context, "Dịch vụ chăm bé ban đêm giúp mẹ ngủ trọn giấc", "Điều dưỡng ở lại xuyên đêm chăm sóc bé — cho bú, thay bỉm, vỗ ợ, dỗ bé ngủ — để mẹ có giấc ngủ liên hoàn phục hồi sức khỏe.", "cham-be-so-sinh", 1_200_000m, 480, "active", "single", nameEn: "Overnight Baby Care Service for Mom's Rest", descriptionEn: "Nurse stays overnight to care for baby — feeding, diaper changes, burping, soothing — so mother can get restorative uninterrupted sleep.");
        var houseSupport = await EnsureServiceAsync(context, "Hỗ trợ việc nhà & dọn dẹp không gian chăm sóc", "Giúp gia đình dọn dẹp, nấu ăn, giặt giũ và sắp xếp không gian sống sạch sẽ để mẹ và bé luôn ở trong môi trường thoải mái, an toàn.", "ho-tro-gia-dinh", 400_000m, 120, "active", "single", nameEn: "Household Support & Care Space Cleaning", descriptionEn: "Help with cleaning, cooking, laundry and organizing living spaces to keep mother and baby in a comfortable, safe environment.");
        var mentalWellness = await EnsureServiceAsync(context, "Tham vấn tâm lý & hỗ trợ tinh thần sau sinh", "Đồng hành cùng mẹ trong giai đoạn nhạy cảm — lắng nghe, chia sẻ và hướng dẫn kỹ thuật thư giãn giúp giảm lo âu, căng thẳng, baby blues.", "ho-tro-tinh-than", 500_000m, 60, "active", "single", nameEn: "Postpartum Psychological Counseling & Emotional Support", descriptionEn: "Accompany mothers through the sensitive period — listening, sharing and guiding relaxation techniques to reduce anxiety, stress and baby blues.");
        var emergencyConsultation = await EnsureServiceAsync(context, "Tư vấn khẩn cấp & xử trí tình huống tại nhà", "Điều dưỡng đến tận nơi hoặc tư vấn trực tuyến khi mẹ gặp tình huống cần xử trí y tế khẩn — sốt cao, chảy máu bất thường, khó thở.", "tu-van-tai-nha", 350_000m, 30, "active", "single", nameEn: "Emergency Consultation & In-Home Care", descriptionEn: "Nurse visits in person or consults online when mother faces urgent medical situations — high fever, abnormal bleeding, difficulty breathing.");
        var miniConsultation = await EnsureServiceAsync(context, "Tư vấn nhanh qua video & điện thoại sau sinh", "Cuộc gọi tư vấn ngắn 15-20 phút với điều dưỡng để gia đình hỏi nhanh cách xử trí các vấn đề thường gặp: tắc sữa, bé quấy khóc, rốn rỉ dịch.", "tu-van-tai-nha", 200_000m, 15, "active", "single", nameEn: "Quick Video & Phone Postnatal Consultation", descriptionEn: "Short 15-20 minute consultation call with a nurse for quick guidance on common issues: blocked ducts, fussy baby, umbilical discharge.");
        var lactationConsult = await EnsureServiceAsync(context, "Tư vấn lợi sữa", "Tư vấn chế độ dinh dưỡng và các phương pháp tự nhiên giúp mẹ kích thích nguồn sữa dồi dào, đảm bảo chất lượng sữa cho bé bú.", "tu-van-tai-nha", 400_000m, 45, "active", "single", nameEn: "Lactation & Milk Supply Consultation", descriptionEn: "Consult on nutrition and natural methods to stimulate abundant milk supply, ensuring quality breast milk for the baby.");
        var blockedMilkDuct = await EnsureServiceAsync(context, "Hỗ trợ tắc tia sữa", "Hỗ trợ mẹ xử lý tình trạng tắc tia sữa bằng kỹ thuật massage nhẹ nhàng, chườm ấm và hướng dẫn cho bú đúng cách để thông tia sữa.", "tu-van-tai-nha", 500_000m, 60, "active", "single", nameEn: "Blocked Milk Duct Relief Support", descriptionEn: "Help mother treat blocked ducts with gentle massage, warm compress and proper breastfeeding guidance to clear the ducts.");
        var poorFeeding = await EnsureServiceAsync(context, "Hỗ trợ bé bú kém", "Đánh giá nguyên nhân bé bú kém — ngậm bắt vú sai, lưỡi ngắn, trào ngược — và hướng dẫn mẹ kỹ thuật cho bú phù hợp.", "cham-be-so-sinh", 400_000m, 60, "active", "single", nameEn: "Poor Feeding Support for Newborns", descriptionEn: "Assess causes of poor feeding — incorrect latch, tongue-tie, reflux — and guide mother with appropriate feeding techniques.");
        var cSectionWound = await EnsureServiceAsync(context, "Chăm sóc vết mổ sau sinh", "Kiểm tra, vệ sinh và theo dõi vết mổ lấy thai — phát hiện sớm dấu hiệu nhiễm trùng, hướng dẫn mẹ chăm sóc vết mổ tại nhà.", "cham-me-sau-sinh", 450_000m, 45, "active", "single", nameEn: "C-Section Wound Care & Monitoring", descriptionEn: "Inspect, clean and monitor cesarean incision — early detection of infection signs, guide mother on home wound care.");
        var lochiaMonitoring = await EnsureServiceAsync(context, "Theo dõi sản dịch sau sinh", "Kiểm tra số lượng, màu sắc, mùi sản dịch — đánh giá tiến trình hồi phục tử cung và phát hiện bất thường sau sinh.", "cham-me-sau-sinh", 350_000m, 30, "active", "single", nameEn: "Postpartum Lochia Monitoring", descriptionEn: "Check lochia amount, color, odor — assess uterine recovery progress and detect postpartum abnormalities.");
        var babySleep = await EnsureServiceAsync(context, "Tư vấn giấc ngủ cho bé", "Hướng dẫn mẹ thiết lập nếp ngủ khoa học cho bé — nhận biết dấu hiệu buồn ngủ, tạo môi trường ngủ an toàn, xử lý khóc đêm và thức giấc.", "cham-be-so-sinh", 350_000m, 45, "active", "single", nameEn: "Baby Sleep Consultation & Training", descriptionEn: "Guide mother on establishing healthy sleep habits — recognize sleepy cues, create safe sleep environment, handle night wakings and fussiness.");

        var pkgDemoBaBuoi = await EnsureServiceAsync(context, "Gói Dùng Thử Trải Nghiệm Chăm Sóc Sau Sinh Tại Nhà", "Gói 3 buổi trải nghiệm nhẹ — tắm bé, theo dõi sức khỏe mẹ và làm quen với quy trình chăm sóc chuyên nghiệp tại nhà, phù hợp gia đình lần đầu sử dụng dịch vụ.", "goi-dich-vu", 790_000m, 60, "active", "package", 3, "mother-health-monitoring,baby-health-monitoring", GeneratePackageScheduleJson(3, "mother-health-monitoring,baby-health-monitoring"), nameEn: "Trial Home Postnatal Care Package", descriptionEn: "3-session light trial — baby bathing, maternal health monitoring and introduction to professional home care, ideal for first-time users.");
        var pkgThongTuyenSua = await EnsureServiceAsync(context, "Gói Hỗ Trợ Tuyến Sữa & Xử Lý Tắc Tia Sữa Sau Sinh", "Gói 3 buổi chuyên sâu — massage khơi thông tuyến sữa, hướng dẫn kỹ thuật cho bú đúng cách, duy trì nguồn sữa mẹ ổn định và ngăn ngừa tắc sữa tái phát.", "goi-dich-vu", 1_800_000m, 90, "active", "package", 3, "breastfeeding-support", GeneratePackageScheduleJson(3, "breastfeeding-support"), nameEn: "Postpartum Milk Duct Support & Blockage Treatment", descriptionEn: "3-session intensive — massage to clear milk ducts, guide proper breastfeeding technique, maintain stable milk supply and prevent recurrence.");
        var pkgGiamNhucMoi = await EnsureServiceAsync(context, "Gói Massage Giảm Nhức Mỏi & Phục Hồi Cơ Thể Sau Sinh", "Gói 6 buổi massage trị liệu — tập trung giảm đau vùng lưng, vai gáy, hông, kết hợp kỹ thuật bấm huyệt giúp mẹ phục hồi thể lực và thư giãn toàn thân.", "goi-dich-vu", 3_600_000m, 60, "active", "package", 6, "postpartum-massage,mother-health-monitoring", GeneratePackageScheduleJson(6, "postpartum-massage,mother-health-monitoring"), nameEn: "Postpartum Pain Relief Massage & Body Recovery", descriptionEn: "6-session therapeutic massage — focus on back, shoulder, neck and hip pain relief with acupressure for full body recovery.");
        var pkgTreSoSinh = await EnsureServiceAsync(context, "Gói Chăm Sóc Toàn Diện Trẻ Sơ Sinh Tại Nhà", "Gói 7 buổi chăm sóc trọn gói cho bé — tắm, vệ sinh rốn, đo chỉ số phát triển, theo dõi phản xạ, hướng dẫn mẹ cách chăm sóc con từng ngày.", "goi-dich-vu", 4_500_000m, 90, "active", "package", 7, "baby-bathing,baby-health-monitoring", GeneratePackageScheduleJson(7, "baby-bathing,baby-health-monitoring"), nameEn: "Comprehensive Newborn Home Care Package", descriptionEn: "7-session all-inclusive baby care — bathing, umbilical hygiene, developmental tracking, reflex monitoring, and daily mother guidance.");
        var pkgMassageTamBe = await EnsureServiceAsync(context, "Gói Massage Phục Hồi Mẹ & Tắm Bé Kết Hợp", "Gói 10 buổi kết hợp massage phục hồi cho mẹ và tắm bé trong một lịch hẹn — tiết kiệm thời gian, chăm sóc cả hai cùng lúc.", "goi-dich-vu", 3_200_000m, 90, "active", "package", 10, "postpartum-massage,baby-bathing", GeneratePackageScheduleJson(10, "postpartum-massage,baby-bathing"), nameEn: "Combined Massage & Baby Bathing Package", descriptionEn: "10-session combined package — postpartum recovery massage for mother and baby bath in one appointment, saving time while caring for both.");
        var pkgPhucHoi = await EnsureServiceAsync(context, "Gói Phục Hồi Sức Khỏe & Tinh Thần Sau Sinh Toàn Diện", "Gói 12 buổi toàn diện — massage phục hồi, tư vấn dinh dưỡng cá nhân hóa, theo dõi sức khỏe mẹ và tham vấn tâm lý giúp mẹ vượt qua giai đoạn hậu sản nhẹ nhàng.", "goi-dich-vu", 7_200_000m, 90, "active", "package", 12, "postpartum-massage,nutrition-consultation,mother-health-monitoring", GeneratePackageScheduleJson(12, "postpartum-massage,nutrition-consultation,mother-health-monitoring"), nameEn: "Comprehensive Postnatal Recovery Package", descriptionEn: "12-session comprehensive — recovery massage, personalized nutrition, maternal health monitoring and psychological counseling for gentle postpartum recovery.");
        var pkgVipSauSinh = await EnsureServiceAsync(context, "Gói VIP Chăm Sóc Toàn Diện Mẹ & Bé Sau Sinh", "Gói 15 buổi cao cấp — massage, tắm bé, theo dõi sức khỏe mẹ & bé, dinh dưỡng, tham vấn tinh thần và tư vấn cho bú — trọn bộ phục hồi sau sinh.", "goi-dich-vu", 14_500_000m, 120, "active", "package", 15, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,mental-wellness", GeneratePackageScheduleJson(15, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,mental-wellness"), nameEn: "VIP Comprehensive Postnatal Care for Mother & Baby", descriptionEn: "15-session premium — massage, baby bath, mother & baby health monitoring, nutrition, mental wellness and breastfeeding support — full postnatal recovery suite.");
        var pkgChuyenSau = await EnsureServiceAsync(context, "Gói Chăm Sóc Chuyên Sâu Mẹ & Bé Toàn Diện", "Gói 18 buổi phục hồi chuyên sâu — massage, tắm bé, khám sức khỏe định kỳ, dinh dưỡng, hỗ trợ cho bú và tham vấn tâm lý — tối ưu cho giai đoạn hậu sản.", "goi-dich-vu", 14_000_000m, 120, "active", "package", 18, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,breastfeeding-support", GeneratePackageScheduleJson(18, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,breastfeeding-support"), nameEn: "Advanced Mother & Baby Comprehensive Care Package", descriptionEn: "18-session intensive recovery — massage, baby bath, regular health checks, nutrition, breastfeeding support and psychological counseling.");
        var pkgChuyenSauBung = await EnsureServiceAsync(context, "Gói Phục Hồi Vóc Dáng & Định Hình Cơ Thể Sau Sinh", "Gói 20 buổi massage định hình chuyên sâu — tập trung phục hồi vùng bụng, eo, hông sau sinh, giúp mẹ lấy lại vóc dáng với lộ trình khoa học và an toàn.", "goi-dich-vu", 14_000_000m, 90, "active", "package", 20, "postpartum-massage,mother-health-monitoring", GeneratePackageScheduleJson(20, "postpartum-massage,mother-health-monitoring"), nameEn: "Postpartum Body Shaping & Figure Recovery Package", descriptionEn: "20-session intensive shaping massage — focus on abdomen, waist and hips postpartum, helping mother regain figure with a safe scientific plan.");
        await DeactivateLegacyServicesAsync(context);

        await context.SaveChangesAsync();

        await FixDuplicatePackagesAsync(context);

        var nurseProfileA = await EnsureNurseProfileAsync(
            context,
            nurseA.Id,
            "7 năm kinh nghiệm tại khoa sản và chăm sóc mẹ bé tại nhà.",
            "Chăm sóc mẹ sau sinh, chăm bé sơ sinh",
            "Cử nhân Điều dưỡng; Chứng chỉ chăm sóc sơ sinh",
            7,
            15,
            4.8m,
            true,
            "verified");

        var nurseProfileB = await EnsureNurseProfileAsync(
            context,
            nurseB.Id,
            "5 năm kinh nghiệm chăm bé sơ sinh và hỗ trợ chăm bé ban đêm.",
            "Chăm bé ban đêm, chăm sóc sơ sinh",
            "Chứng chỉ hành nghề điều dưỡng; Chứng nhận sơ cứu trẻ sơ sinh",
            5,
            12,
            4.6m,
            true,
            "verified");

        var nurseProfilePending = await EnsureNurseProfileAsync(
            context,
            nursePending.Id,
            "Điều dưỡng mới đang chờ xác minh hồ sơ.",
            "Chăm sóc mẹ sau sinh",
            "Ứng viên điều dưỡng",
            2,
            10,
            0m,
            true,
            "unverified");

        // Persist nurse profiles first so dependent rows use valid nurse_profile_id values.
        await context.SaveChangesAsync();

        await EnsureDocumentAsync(context, nurseProfileA.Id, DocumentTypes.IdCardFront, "seed_huong_id", "approved");
        await EnsureDocumentAsync(context, nurseProfileA.Id, DocumentTypes.Certificate, "seed_huong_hospital", "approved");
        await EnsureDocumentAsync(context, nurseProfileB.Id, DocumentTypes.IdCardFront, "seed_mai_id", "approved");
        await EnsureDocumentAsync(context, nurseProfilePending.Id, DocumentTypes.IdCardFront, "seed_ngoc_id", "pending_review");

        await EnsureNurseServiceAsync(context, nurseProfileA.Id, motherHealth.Id, 550_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, babyHealth.Id, 500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, lactation.Id, 650_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, massage.Id, 750_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, nutrition.Id, 580_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, emergencyConsultation.Id, 380_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, miniConsultation.Id, 220_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgDemoBaBuoi.Id, 850_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgThongTuyenSua.Id, 1_950_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgGiamNhucMoi.Id, 3_900_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgPhucHoi.Id, 7_800_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgChuyenSau.Id, 15_000_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, babyHealth.Id, 470_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, nightCare.Id, 150_000m, "hourly", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, massage.Id, 750_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, babyBath.Id, 380_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, mentalWellness.Id, 520_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, houseSupport.Id, 420_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, miniConsultation.Id, 220_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgDemoBaBuoi.Id, 890_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgMassageTamBe.Id, 3_400_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgTreSoSinh.Id, 4_800_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgVipSauSinh.Id, 15_500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgChuyenSauBung.Id, 15_000_000m, "fixed", "enabled");

        await EnsureDaNangNurseSeedAsync(
            context,
            userManager,
            [
                babyBath,
                motherHealth,
                babyHealth,
                lactation,
                massage,
                nutrition,
                nightCare,
                mentalWellness,
                houseSupport,
                emergencyConsultation,
                miniConsultation,
                pkgDemoBaBuoi,
                pkgTreSoSinh,
                pkgMassageTamBe,
                pkgPhucHoi
            ]);

        // Use a stable reference date so re-running seed doesn't create duplicate slots.
        // "today" is always the current UTC date; past slots are fixed, future slots shift forward.
        var today = DateTime.UtcNow.Date;

        // Clean up old seed availability slots (past) to avoid unbounded growth.
        var oldSlots = context.AvailabilitySlots
            .Where(s => s.EndTime < today && !context.Bookings.Any(b => b.AvailabilitySlotId == s.Id))
            .ToList();
        if (oldSlots.Count > 0)
        {
            context.AvailabilitySlots.RemoveRange(oldSlots);
        }
        // mamay
        // Nurse A: one past + two future
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(-2).AddHours(8), today.AddDays(-2).AddHours(12));
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(1).AddHours(8), today.AddDays(1).AddHours(12));
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(2).AddHours(13), today.AddDays(2).AddHours(17));

        // Nurse B: one past + two future
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(-1).AddHours(20), today.AddDays(0).AddHours(4));
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(1).AddHours(20), today.AddDays(2).AddHours(4));
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(3).AddHours(8), today.AddDays(3).AddHours(12));

        await context.SaveChangesAsync();

        var completedBooking = await EnsureBookingAsync(
            context,
            "seed:booking:completed",
            customerA.Id,
            nurseA.Id,
            motherHealth.Id,
            BookingStatuses.Completed,
            550_000m,
            "25 Nguyễn Văn Linh, quận Hải Châu, Đà Nẵng",
            today.AddDays(-2).AddHours(9),
            today.AddDays(-2).AddHours(11));

        var pendingBooking = await EnsureBookingAsync(
            context,
            "seed:booking:pending",
            customerB.Id,
            nurseA.Id,
            babyHealth.Id,
            BookingStatuses.PendingConfirm,
            500_000m,
            "120 Hồ Nghinh, quận Sơn Trà, Đà Nẵng",
            today.AddDays(1).AddHours(9),
            today.AddDays(1).AddHours(11));

        var inProgressBooking = await EnsureBookingAsync(
            context,
            "seed:booking:inprogress",
            customerA.Id,
            nurseB.Id,
            nightCare.Id,
            BookingStatuses.InProgress,
            2_000_000m,
            "25 Nguyễn Văn Linh, quận Hải Châu, Đà Nẵng",
            today.AddDays(-1).AddHours(21),
            today.AddDays(0).AddHours(3));

        await context.SaveChangesAsync();

        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.PendingConfirm, customerA.Id, "seed-created", today.AddDays(-3).AddHours(8));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.Confirmed, nurseA.Id, "seed-confirmed", today.AddDays(-3).AddHours(9));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.InProgress, nurseA.Id, "seed-started", today.AddDays(-2).AddHours(9));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.Completed, nurseA.Id, "seed-completed", today.AddDays(-2).AddHours(11));

        await EnsureBookingStatusHistoryAsync(context, pendingBooking.Id, BookingStatuses.PendingConfirm, customerB.Id, "seed-created", today.AddHours(8));

        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.PendingConfirm, customerA.Id, "seed-created", today.AddDays(-2).AddHours(18));
        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.Confirmed, nurseB.Id, "seed-confirmed", today.AddDays(-2).AddHours(19));
        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.InProgress, nurseB.Id, "seed-started", today.AddDays(-1).AddHours(21));

        await EnsurePaymentAsync(context, completedBooking.Id, 550_000m, "bank_transfer", PaymentStatuses.Paid, "SEED-TXN-PAID-001");
        await EnsurePaymentAsync(context, pendingBooking.Id, 500_000m, "bank_transfer", PaymentStatuses.Initiated, "SEED-TXN-INIT-002");
        await EnsurePaymentAsync(context, inProgressBooking.Id, 2_000_000m, "bank_transfer", PaymentStatuses.Paid, "SEED-TXN-PAID-003");

        await EnsurePayoutAsync(context, completedBooking.Id, nurseA.Id, 500_000m, 50_000m, "released", today.AddDays(-1));
        await EnsurePayoutAsync(context, inProgressBooking.Id, nurseB.Id, 1_800_000m, 200_000m, "on_hold", null);

        await EnsureReviewAsync(context, completedBooking.Id, customerA.Id, nurseA.Id, 5, "Hỗ trợ rất tận tâm và chu đáo.", today.AddDays(-2).AddHours(12));

        await EnsureDisputeAsync(context, inProgressBooking.Id, "Cần làm rõ chi phí phát sinh ngoài giờ", "open", "Đang chờ quản trị viên xem xét");

        var conversation = await EnsureConversationAsync(context, completedBooking.Id, customerA.Id, nurseA.Id);
        await context.SaveChangesAsync();
        await EnsureChatMessageAsync(context, conversation.Id, customerA.Id, "Chị đến giúp em lúc 9h nhé.", false, today.AddDays(-2).AddHours(8));
        await EnsureChatMessageAsync(context, conversation.Id, nurseA.Id, "Dạ em đến đúng giờ ạ.", true, today.AddDays(-2).AddHours(8).AddMinutes(5));

        await EnsureNotificationAsync(context, customerA.Id, "Lịch hẹn đã hoàn thành", "Lịch hẹn của bạn đã được hoàn thành thành công.", "booking");
        await EnsureNotificationAsync(context, nurseA.Id, "Đánh giá mới", "Bạn vừa nhận được một đánh giá 5 sao.", "review");
        await EnsureNotificationAsync(context, admin.Id, "Khiếu nại đang mở", "Có một khiếu nại đang chờ quản trị viên xem xét.", "system");

        await context.SaveChangesAsync();

        logger.LogInformation("Seed data completed successfully.");
    }

    private static async Task EnsureRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        await EnsureRoleAsync(roleManager, AppRoles.Admin, "Administrator");
        await EnsureRoleAsync(roleManager, AppRoles.Customer, "Customer");
        await EnsureRoleAsync(roleManager, AppRoles.Nurse, "Nurse");
        await EnsureRoleAsync(roleManager, AppRoles.NurseUnconfirmed, "Nurse (Unconfirmed)");
        await EnsureRoleAsync(roleManager, AppRoles.NurseConfirmed, "Nurse (Confirmed)");
    }

    private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string code, string displayName)
    {
        var normalizedCode = roleManager.NormalizeKey(code);
        var role = await roleManager.FindByNameAsync(code);
        role ??= await roleManager.Roles.FirstOrDefaultAsync(r => r.Name == code);

        if (role == null)
        {
            var createResult = await roleManager.CreateAsync(new ApplicationRole { Name = code, DisplayName = displayName });
            if (createResult.Succeeded)
            {
                return;
            }

            role = await roleManager.Roles.FirstOrDefaultAsync(r => r.Name == code);
            if (role == null)
            {
                throw new InvalidOperationException(
                    $"Unable to create seed role '{code}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var changed = false;
        if (role.Name != code)
        {
            role.Name = code;
            changed = true;
        }

        if (role.DisplayName != displayName)
        {
            role.DisplayName = displayName;
            changed = true;
        }

        if (role.NormalizedName != normalizedCode)
        {
            role.NormalizedName = normalizedCode;
            changed = true;
        }

        if (changed)
        {
            var updateResult = await roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to update seed role '{code}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string phone,
        string[] roles,
        string seedPassword = DefaultPassword,
        bool resetPassword = false)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phone,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = seedPassword == DefaultPassword
                ? await userManager.CreateAsync(user, seedPassword)
                : await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create seed user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            if (seedPassword != DefaultPassword)
            {
                await SetSeedPasswordAsync(userManager, user, seedPassword);
            }
        }
        else
        {
            var changed = false;

            if (user.FullName != fullName)
            {
                user.FullName = fullName;
                changed = true;
            }

            if (user.PhoneNumber != phone)
            {
                user.PhoneNumber = phone;
                changed = true;
            }

            if (user.UserName != email)
            {
                user.UserName = email;
                changed = true;
            }

            if (user.Status != "active")
            {
                user.Status = "active";
                changed = true;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                changed = true;
            }

            if (changed)
            {
                user.UpdatedAt = DateTime.UtcNow;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to update seed user '{email}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        if (resetPassword && !await userManager.CheckPasswordAsync(user, seedPassword))
        {
            await SetSeedPasswordAsync(userManager, user, seedPassword);
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            if (!existingRoles.Contains(role))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, role);
                if (!addRoleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to assign role '{role}' to '{email}'.");
                }
            }
        }

        return user;
    }

    private static async Task SetSeedPasswordAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password)
    {
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException($"Unable to set seed password for '{user.Email}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task EnsureAddressAsync(
        MomCareContext context,
        int userId,
        string fullAddress,
        string? ward,
        string? district,
        bool isDefault,
        string type,
        double? latitude = null,
        double? longitude = null)
    {
        var address = await context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.Type == type && a.IsDefault == isDefault);
        if (address == null)
        {
            context.Addresses.Add(new Address
            {
                UserId = userId,
                FullAddress = fullAddress,
                Ward = ward,
                District = district,
                Latitude = latitude,
                Longitude = longitude,
                IsDefault = isDefault,
                Type = type
            });
            return;
        }
    }

    private static async Task EnsureDaNangNurseSeedAsync(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        Service[] services)
    {
        var districts = new[]
        {
            new { Name = "Hải Châu", Lat = 16.0678, Lng = 108.2208 },
            new { Name = "Thanh Khê", Lat = 16.0707, Lng = 108.1906 },
            new { Name = "Sơn Trà", Lat = 16.1062, Lng = 108.2529 },
            new { Name = "Ngũ Hành Sơn", Lat = 16.0037, Lng = 108.2647 },
            new { Name = "Liên Chiểu", Lat = 16.0744, Lng = 108.1491 },
            new { Name = "Cẩm Lệ", Lat = 16.0169, Lng = 108.2047 },
            new { Name = "Hòa Vang", Lat = 16.0390, Lng = 108.1135 },
        };

        var names = new[]
        {
            "Nguyễn Thị An", "Trần Thị Bình", "Lê Minh Chi", "Phạm Thu Dung", "Võ Hương Giang", "Hoàng Ngọc Hà",
            "Đặng Mỹ Hạnh", "Bùi Thanh Hoa", "Ngô Gia Khánh", "Đỗ Mai Lan", "Huỳnh Thảo Linh", "Phan Nhật Mai",
            "Nguyễn Kim Minh", "Trần Hà My", "Lê Thanh Nga", "Phạm Bảo Ngân", "Võ Hồng Ngọc", "Hoàng Yến Nhi",
            "Đặng Thị Oanh", "Bùi Minh Phương", "Ngô Như Quỳnh", "Đỗ Phương Thảo", "Huỳnh Anh Thu", "Phan Huyền Trang",
            "Nguyễn Mai Trinh", "Trần Cẩm Tuyền", "Lê Khánh Uyên", "Phạm Ngọc Vân", "Võ Tường Vy", "Hoàng Thị Yến",
            "Đặng Lan Anh", "Bùi Diễm My", "Ngô Thu Hiền", "Đỗ Hồng Hương", "Huỳnh Gia Kim", "Phan Bích Loan",
            "Nguyễn Quỳnh Như", "Trần Minh Tâm", "Lê Phương Thanh", "Phạm Thị Thủy", "Võ Ngọc Trà", "Hoàng Anh Tú"
        };

        var locations = new[]
        {
            new { FullAddress = "Chợ Hàn Đà Nẵng, 116 Bạch Đằng, Hải Châu 1, Hải Châu, Đà Nẵng", Ward = "Hải Châu 1", District = "Hải Châu", Latitude = 16.068244159000074, Longitude = 108.22487182000003, Radius = 6 },
            new { FullAddress = "Chợ Cồn, Hùng Vương, Hải Châu 2, Hải Châu, Đà Nẵng", Ward = "Hải Châu 2", District = "Hải Châu", Latitude = 16.0680564, Longitude = 108.2143082, Radius = 6 },
            new { FullAddress = "Cầu Rồng, An Hải Tây, Sơn Trà, Đà Nẵng", Ward = "An Hải Tây", District = "Hải Châu", Latitude = 16.061082357512515, Longitude = 108.22786173973377, Radius = 7 },
            new { FullAddress = "Bệnh viện Đa khoa Quốc tế Vinmec Đà Nẵng, 4 đường 30 Tháng 4, Hòa Cường Bắc, Hải Châu, Đà Nẵng", Ward = "Hòa Cường Bắc", District = "Hải Châu", Latitude = 16.03940394500006, Longitude = 108.21151897400006, Radius = 8 },
            new { FullAddress = "Nguyễn Văn Linh, Phước Ninh, Hải Châu, Đà Nẵng", Ward = "Phước Ninh", District = "Hải Châu", Latitude = 16.0609273, Longitude = 108.2189461, Radius = 7 },
            new { FullAddress = "Duy Tân, Hòa Thuận Tây, Hải Châu, Đà Nẵng", Ward = "Hòa Thuận Tây", District = "Hải Châu", Latitude = 16.0504684, Longitude = 108.2095168, Radius = 8 },
            new { FullAddress = "Hàm Nghi, Thạc Gián, Thanh Khê, Đà Nẵng", Ward = "Thạc Gián", District = "Thanh Khê", Latitude = 16.0628962, Longitude = 108.2107061, Radius = 6 },
            new { FullAddress = "Điện Biên Phủ, Thanh Khê Đông, Thanh Khê, Đà Nẵng", Ward = "Thanh Khê Đông", District = "Thanh Khê", Latitude = 16.065761, Longitude = 108.1924117, Radius = 7 },
            new { FullAddress = "Lê Duẩn, Chính Gián, Thanh Khê, Đà Nẵng", Ward = "Chính Gián", District = "Thanh Khê", Latitude = 16.069294, Longitude = 108.2095453, Radius = 6 },
            new { FullAddress = "Nguyễn Tất Thành, Xuân Hà, Thanh Khê, Đà Nẵng", Ward = "Xuân Hà", District = "Thanh Khê", Latitude = 16.0776, Longitude = 108.1889, Radius = 8 },
            new { FullAddress = "Trần Cao Vân, Thanh Khê, Đà Nẵng", Ward = "Thanh Khê", District = "Thanh Khê", Latitude = 16.0707, Longitude = 108.2006, Radius = 7 },
            new { FullAddress = "Mẹ Nhu, Thanh Khê Tây, Thanh Khê, Đà Nẵng", Ward = "Thanh Khê Tây", District = "Thanh Khê", Latitude = 16.0748, Longitude = 108.1797, Radius = 8 },
            new { FullAddress = "Cầu Sông Hàn, An Hải Bắc, Sơn Trà, Đà Nẵng", Ward = "An Hải Bắc", District = "Sơn Trà", Latitude = 16.072319367279547, Longitude = 108.22772329782113, Radius = 7 },
            new { FullAddress = "Phạm Văn Đồng, An Hải Bắc, Sơn Trà, Đà Nẵng", Ward = "An Hải Bắc", District = "Sơn Trà", Latitude = 16.070559509819137, Longitude = 108.23706079591314, Radius = 7 },
            new { FullAddress = "Hồ Nghinh, Phước Mỹ, Sơn Trà, Đà Nẵng", Ward = "Phước Mỹ", District = "Sơn Trà", Latitude = 16.06676487624661, Longitude = 108.24317303171603, Radius = 6 },
            new { FullAddress = "A La Carte Danang Beach, Võ Nguyên Giáp, Phước Mỹ, Sơn Trà, Đà Nẵng", Ward = "Phước Mỹ", District = "Sơn Trà", Latitude = 16.0687309, Longitude = 108.244693, Radius = 6 },
            new { FullAddress = "Phường Mân Thái, Sơn Trà, Đà Nẵng", Ward = "Mân Thái", District = "Sơn Trà", Latitude = 16.08934956125003, Longitude = 108.24009102975006, Radius = 8 },
            new { FullAddress = "Phường Thọ Quang, Sơn Trà, Đà Nẵng", Ward = "Thọ Quang", District = "Sơn Trà", Latitude = 16.13084410325007, Longitude = 108.2500609712501, Radius = 10 },
            new { FullAddress = "Võ Nguyên Giáp, Mỹ An, Ngũ Hành Sơn, Đà Nẵng", Ward = "Mỹ An", District = "Ngũ Hành Sơn", Latitude = 16.052298235401214, Longitude = 108.24817960245824, Radius = 7 },
            new { FullAddress = "Lê Văn Hiến, Khuê Mỹ, Ngũ Hành Sơn, Đà Nẵng", Ward = "Khuê Mỹ", District = "Ngũ Hành Sơn", Latitude = 16.0178054, Longitude = 108.2535484, Radius = 8 },
            new { FullAddress = "Trần Đại Nghĩa, Hòa Hải, Ngũ Hành Sơn, Đà Nẵng", Ward = "Hòa Hải", District = "Ngũ Hành Sơn", Latitude = 15.9851814, Longitude = 108.2566269, Radius = 9 },
            new { FullAddress = "298 Võ Nguyên Giáp, Mỹ An, Ngũ Hành Sơn, Đà Nẵng", Ward = "Mỹ An", District = "Ngũ Hành Sơn", Latitude = 16.0503276, Longitude = 108.2484905, Radius = 7 },
            new { FullAddress = "Phường Khuê Mỹ, Ngũ Hành Sơn, Đà Nẵng", Ward = "Khuê Mỹ", District = "Ngũ Hành Sơn", Latitude = 16.02810771350005, Longitude = 108.24825368650005, Radius = 8 },
            new { FullAddress = "Bãi biển Mỹ Khê, Võ Nguyên Giáp, Đà Nẵng", Ward = "Phước Mỹ", District = "Ngũ Hành Sơn", Latitude = 16.060879, Longitude = 108.2466345, Radius = 7 },
            new { FullAddress = "Mê Linh, Hòa Khánh Bắc, Liên Chiểu, Đà Nẵng", Ward = "Hòa Khánh Bắc", District = "Liên Chiểu", Latitude = 16.087888114927807, Longitude = 108.12273119146593, Radius = 9 },
            new { FullAddress = "Nguyễn Lương Bằng, Hòa Hiệp Nam, Liên Chiểu, Đà Nẵng", Ward = "Hòa Hiệp Nam", District = "Liên Chiểu", Latitude = 16.09610569448002, Longitude = 108.13736368966741, Radius = 9 },
            new { FullAddress = "Tôn Đức Thắng, Hòa Minh, Liên Chiểu, Đà Nẵng", Ward = "Hòa Minh", District = "Liên Chiểu", Latitude = 16.05987818592002, Longitude = 108.1633243119634, Radius = 8 },
            new { FullAddress = "Hòa Khánh, Đà Nẵng", Ward = "Hòa Khánh", District = "Liên Chiểu", Latitude = 16.0554759, Longitude = 108.1250791, Radius = 9 },
            new { FullAddress = "Nam Ô, Hòa Hiệp Nam, Liên Chiểu, Đà Nẵng", Ward = "Hòa Hiệp Nam", District = "Liên Chiểu", Latitude = 16.1057327, Longitude = 108.136637, Radius = 9 },
            new { FullAddress = "Âu Cơ, Hòa Khánh Bắc, Liên Chiểu, Đà Nẵng", Ward = "Hòa Khánh Bắc", District = "Liên Chiểu", Latitude = 16.0735, Longitude = 108.1356, Radius = 9 },
            new { FullAddress = "Cách Mạng Tháng 8, Khuê Trung, Cẩm Lệ, Đà Nẵng", Ward = "Khuê Trung", District = "Cẩm Lệ", Latitude = 16.018453552910724, Longitude = 108.20753793646035, Radius = 7 },
            new { FullAddress = "Ông Ích Đường, Khuê Trung, Cẩm Lệ, Đà Nẵng", Ward = "Khuê Trung", District = "Cẩm Lệ", Latitude = 16.019805848793826, Longitude = 108.20334397595265, Radius = 7 },
            new { FullAddress = "Hòa Xuân, Cẩm Lệ, Đà Nẵng", Ward = "Hòa Xuân", District = "Cẩm Lệ", Latitude = 15.978817598750027, Longitude = 108.20333914625007, Radius = 8 },
            new { FullAddress = "Nguyễn Phước Lan, Hòa Xuân, Cẩm Lệ, Đà Nẵng", Ward = "Hòa Xuân", District = "Cẩm Lệ", Latitude = 15.9879, Longitude = 108.2072, Radius = 8 },
            new { FullAddress = "Lê Đại Hành, Hòa Thọ Đông, Cẩm Lệ, Đà Nẵng", Ward = "Hòa Thọ Đông", District = "Cẩm Lệ", Latitude = 16.02272535041191, Longitude = 108.20212317496076, Radius = 7 },
            new { FullAddress = "Chợ Cẩm Lệ, Hoàng Xuân Hãn, Khuê Trung, Cẩm Lệ, Đà Nẵng", Ward = "Khuê Trung", District = "Cẩm Lệ", Latitude = 16.014031286000034, Longitude = 108.20693976000007, Radius = 7 },
            new { FullAddress = "Trung tâm hành chính Hòa Vang, Đà Nẵng", Ward = "Hòa Phong", District = "Hòa Vang", Latitude = 15.9783739, Longitude = 108.0361555, Radius = 12 },
            new { FullAddress = "Túy Loan, Hòa Phong, Hòa Vang, Đà Nẵng", Ward = "Hòa Phong", District = "Hòa Vang", Latitude = 15.9878, Longitude = 108.1267, Radius = 12 },
            new { FullAddress = "Bà Nà Hills, thôn An Sơn, Hòa Ninh, Hòa Vang, Đà Nẵng", Ward = "Hòa Ninh", District = "Hòa Vang", Latitude = 15.997725143000025, Longitude = 107.98802187400008, Radius = 15 },
            new { FullAddress = "Hòa Phú, Hòa Vang, Đà Nẵng", Ward = "Hòa Phú", District = "Hòa Vang", Latitude = 15.9783739, Longitude = 108.0361555, Radius = 12 },
            new { FullAddress = "Xã Hòa Tiến, Hòa Vang, Đà Nẵng", Ward = "Hòa Tiến", District = "Hòa Vang", Latitude = 15.969297787000073, Longitude = 108.18080644000008, Radius = 10 },
            new { FullAddress = "Xã Hòa Nhơn, Hòa Vang, Đà Nẵng", Ward = "Hòa Nhơn", District = "Hòa Vang", Latitude = 16.00045612400004, Longitude = 108.14523418900006, Radius = 11 },
        };

        var today = DateTime.UtcNow.Date;
        var serviceCount = services.Length;

        for (var districtIndex = 0; districtIndex < locations.Length / 6; districtIndex++)
        {
            var district = districts[districtIndex];

            for (var localIndex = 0; localIndex < 6; localIndex++)
            {
                var index = districtIndex * 6 + localIndex;
                var location = locations[index];
                var number = index + 1;
                var email = $"danang.nurse{number:00}@momcare.local";
                var phone = $"0918{number:000000}";
                var fullName = names[index];
                var user = await EnsureUserAsync(userManager, email, fullName, phone, [AppRoles.NurseConfirmed]);
                var latOffset = ((localIndex % 3) - 1) * 0.008 + districtIndex * 0.0006;
                var lngOffset = ((localIndex / 3) - 0.5) * 0.01 - districtIndex * 0.0004;

                await EnsureAddressAsync(
                    context,
                    user.Id,
                    location.FullAddress,
                    location.Ward,
                    location.District,
                    true,
                    "nurse_base",
                    location.Latitude,
                    location.Longitude);

                var experience = 1 + (index * 3 % 12);
                var radius = 5 + (index % 8);
                var rating = Math.Round(4.0m + ((index * 7) % 10) / 10m, 1);
                var profile = await EnsureNurseProfileAsync(
                    context,
                    user.Id,
                    $"Điều dưỡng chăm sóc mẹ và bé tại nhà khu vực {district.Name}, Đà Nẵng. Hồ sơ đã được CareMate xác minh.",
                    index % 3 == 0 ? "Chăm sóc mẹ sau sinh, chăm bé sơ sinh" : index % 3 == 1 ? "Tắm bé, hỗ trợ cho bú" : "Phục hồi sau sinh, tư vấn dinh dưỡng",
                    "Chứng chỉ điều dưỡng; Chứng nhận chăm sóc tại nhà",
                    experience,
                    radius,
                    rating,
                    true,
                    "verified");
                profile.ServiceRadiusKm = location.Radius;
                profile.Bio = $"Điều dưỡng chăm sóc mẹ và bé tại nhà khu vực {location.District}, Đà Nẵng. Hồ sơ đã được CareMate xác minh.";

                await context.SaveChangesAsync();

                await EnsureDocumentAsync(context, profile.Id, DocumentTypes.IdCardFront, $"seed_danang_{number:00}_id", "approved");
                await EnsureDocumentAsync(context, profile.Id, DocumentTypes.Certificate, $"seed_danang_{number:00}_cert", "approved");

                var enabledServiceIndexes = new[]
                {
                    index % serviceCount,
                    (index + 2) % serviceCount,
                    (index + 5) % serviceCount,
                    (index + districtIndex + 8) % serviceCount,
                };

                foreach (var serviceIndex in enabledServiceIndexes.Distinct())
                {
                    var service = services[serviceIndex];
                    var priceVariance = (index % 5) * 25_000m;
                    var servicePrice = Math.Max(29_000m, service.BasePrice + priceVariance - (districtIndex % 3) * 15_000m);
                    await EnsureNurseServiceAsync(context, profile.Id, service.Id, servicePrice, service.ServiceKind == "package" ? "fixed" : "fixed", "enabled");
                }

                var firstStart = today.AddDays(1 + (index % 6)).AddHours(8 + (localIndex % 3) * 2);
                var secondStart = today.AddDays(2 + (index % 7)).AddHours(13 + (districtIndex % 3));
                await EnsureAvailabilitySlotAsync(context, profile.Id, firstStart, firstStart.AddHours(4));
                await EnsureAvailabilitySlotAsync(context, profile.Id, secondStart, secondStart.AddHours(4));
            }
        }
    }

    private static async Task<Service> EnsureServiceAsync(
        MomCareContext context,
        string name,
        string description,
        string category,
        decimal basePrice,
        int durationMinutes,
        string status,
        string serviceKind,
        int? packageDays = null,
        string? includedServiceKeys = null,
        string? packageScheduleJson = null,
        string? nameEn = null,
        string? descriptionEn = null)
    {
        var service = await context.Services.FirstOrDefaultAsync(s => s.Name == name);
        if (service == null)
        {
            service = new Service
            {
                Name = name,
                Category = category,
                Description = description,
                NameEn = nameEn,
                DescriptionEn = descriptionEn,
                BasePrice = basePrice,
                EstimatedDurationMinutes = durationMinutes,
                ServiceKind = serviceKind,
                PackageDays = packageDays,
                IncludedServiceKeys = includedServiceKeys,
                PackageScheduleJson = packageScheduleJson,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            context.Services.Add(service);
            return service;
        }

        service.Category = category;
        service.Description = description;
        service.NameEn = nameEn ?? service.NameEn;
        service.DescriptionEn = descriptionEn ?? service.DescriptionEn;
        service.BasePrice = basePrice;
        service.EstimatedDurationMinutes = durationMinutes;
        service.ServiceKind = serviceKind;
        service.PackageDays = packageDays;
        service.IncludedServiceKeys = includedServiceKeys;
        service.PackageScheduleJson = packageScheduleJson;
        service.Status = status;

        return service;
    }

    private static string GeneratePackageScheduleJson(int days, string serviceKeys)
    {
        var list = new System.Collections.Generic.List<object>();
        var labels = serviceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GetPackageServiceLabel)
            .ToList();
        var serviceSummary = labels.Count > 0 ? string.Join(", ", labels) : "chăm sóc tổng quát";

        for (int i = 1; i <= days; i++)
        {
            string title;
            string desc;

            if (i == 1)
            {
                title = "Khởi đầu chăm sóc";
                desc = "Khám đánh giá tổng quát ngày đầu tiên và thiết lập phác đồ chăm sóc phù hợp.";
            }
            else if (i == days)
            {
                title = "Buổi cuối – tổng kết";
                desc = "Thực hiện dịch vụ buổi cuối, hướng dẫn gia đình tự chăm sóc sau khi kết thúc gói.";
            }
            else if (i % 3 == 0)
            {
                title = "Đánh giá định kỳ";
                desc = "Chăm sóc theo liệu trình, đồng thời kiểm tra tiến độ phục hồi của mẹ và bé.";
            }
            else
            {
                title = $"Chăm sóc ngày {i}";
                desc = "Thực hiện các dịch vụ theo liệu trình hàng ngày để đảm bảo sức khỏe và sự thư giãn.";
            }

            if (i == 1)
            {
                title = "Khởi động và đánh giá ban đầu";
                desc = $"Y tá đánh giá tình trạng mẹ và bé, thống nhất mục tiêu chăm sóc, sau đó thực hiện các hạng mục chính: {serviceSummary}.";
            }
            else if (i == days)
            {
                title = "Tổng kết và bàn giao hướng dẫn";
                desc = $"Hoàn tất liệu trình {serviceSummary}, tổng kết thay đổi sau gói và hướng dẫn gia đình tiếp tục theo dõi tại nhà.";
            }
            else if (i % 3 == 0)
            {
                title = $"Ngày {i}: Theo dõi tiến triển";
                desc = $"Thực hiện {BuildDailyServiceSummary(serviceKeys, i)}, ghi nhận phản hồi của gia đình và điều chỉnh nhịp chăm sóc nếu cần.";
            }
            else
            {
                title = $"Ngày {i}: Chăm sóc theo liệu trình";
                desc = $"Tập trung vào {BuildDailyServiceSummary(serviceKeys, i)} để duy trì tiến độ chăm sóc ổn định cho mẹ và bé.";
            }

            list.Add(new
            {
                day = i,
                title = title,
                description = desc,
                serviceKeys = serviceKeys
            });
        }
        return System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
    }

    private static string BuildDailyServiceSummary(string serviceKeys, int day)
    {
        var keys = serviceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (keys.Count == 0)
        {
            return "kiểm tra sức khỏe, vệ sinh cơ bản và tư vấn chăm sóc tại nhà";
        }

        var ordered = keys
            .Skip((day - 1) % keys.Count)
            .Concat(keys.Take((day - 1) % keys.Count))
            .Take(Math.Min(3, keys.Count))
            .Select(GetPackageServiceDetail);

        return string.Join("; ", ordered);
    }

    private static string GetPackageServiceLabel(string key)
    {
        return key switch
        {
            "baby-bathing" => "tắm bé",
            "mother-health-monitoring" => "theo dõi sức khỏe mẹ",
            "baby-health-monitoring" => "theo dõi sức khỏe bé",
            "breastfeeding-support" => "hỗ trợ cho bú",
            "postpartum-massage" => "massage phục hồi sau sinh",
            "nutrition-consultation" => "tư vấn dinh dưỡng",
            "mental-wellness" => "hỗ trợ tâm lý",
            "night-care" => "chăm bé ban đêm",
            "house-support" => "hỗ trợ việc nhà nhẹ",
            _ => key
        };
    }

    private static string GetPackageServiceDetail(string key)
    {
        return key switch
        {
            "baby-bathing" => "tắm bé, vệ sinh rốn/da và hướng dẫn thao tác an toàn",
            "mother-health-monitoring" => "kiểm tra dấu hiệu hồi phục của mẹ, vết mổ/vết khâu và mức độ đau",
            "baby-health-monitoring" => "theo dõi thân nhiệt, ăn ngủ, tiểu tiện/tiêu phân và dấu hiệu bất thường của bé",
            "breastfeeding-support" => "kiểm tra khớp ngậm, tư thế cho bú và xử lý căng/tắc tia sữa",
            "postpartum-massage" => "massage thư giãn, giảm nhức mỏi lưng vai gáy và hỗ trợ lưu thông máu",
            "nutrition-consultation" => "tư vấn thực đơn, bổ sung nước/sữa và các món phù hợp giai đoạn sau sinh",
            "mental-wellness" => "lắng nghe tình trạng cảm xúc, hướng dẫn nghỉ ngơi và dấu hiệu cần hỗ trợ thêm",
            "night-care" => "hỗ trợ bé ngủ, bú đêm và ghi nhận những thay đổi trong đêm",
            "house-support" => "sắp xếp khu chăm sóc, khu tiệt trùng và các việc nhẹ quanh mẹ và bé",
            _ => key
        };
    }

    private static async Task DeactivateLegacyServicesAsync(MomCareContext context)
    {
        var legacyNames = new[]
        {
            "Chăm sóc mẹ sau sinh tại nhà",
            "Chăm sóc bé sơ sinh tại nhà",
            "Tư vấn hỗ trợ cho bé bú",
            "Massage phục hồi sau sinh",
            "Theo dõi vết mổ sau sinh",
            "Tư vấn dinh dưỡng sau sinh",
            "Tắm bé tại nhà",
            "Hỗ trợ tinh thần sau sinh",
            "Chăm sóc bầu ngực sau sinh",
            "Hướng dẫn xây dựng nếp ngủ cho bé",
            // Legacy English packages
            "Basic Package",
            "Standard Package",
            "Premium Package",
            // Previous names (renamed in this migration)
            "Theo dõi sức khỏe mẹ",
            "Theo dõi sức khỏe bé",
            "Massage sau sinh",
            "Hỗ trợ tâm lý",
            "Tư vấn khẩn",
            "Gói Mini Tư Vấn Nhanh",
            "Gói Demo Chăm Sóc 3 Buổi",
            "Gói Chăm Sóc Thông Tuyến Sữa",
            "Gói Chăm Sóc Bầu Giảm Nhức Mỏi",
            "Gói Chăm Sóc Trẻ Sơ Sinh",
            "Gói Chăm Sóc Phục Hồi",
            "Gói Chăm Sóc VIP Sau Sinh",
            "Gói Chăm Sóc Chuyên Sâu",
            "Gói Chăm Sóc Chuyên Sâu Bụng & Chăm Sóc Da",
            // Second round — even more specific names (2026-06)
            "Tắm bé",
            "Theo dõi phục hồi mẹ",
            "Theo dõi phát triển bé",
            "Hỗ trợ cho bú",
            "Massage phục hồi",
            "Tư vấn dinh dưỡng",
            "Chăm bé ban đêm",
            "Hỗ trợ việc nhà",
            "Tham vấn tinh thần",
            "Tư vấn nhanh",
            "Gói Dùng Thử Chăm Sóc Tại Nhà",
            "Gói Hỗ Trợ Tuyến Sữa",
            "Gói Massage Giảm Nhức Mỏi",
            "Gói Chăm Sóc Sơ Sinh",
            "Gói Massage & Tắm Bé",
            "Gói Phục Hồi Sau Sinh",
            "Gói VIP Chăm Sóc Sau Sinh",
            "Gói Chăm Sóc Toàn Diện",
            "Gói Phục Hồi Vóc Dáng",
        };

        var legacyServices = await context.Services
            .Where(service => legacyNames.Contains(service.Name))
            .ToListAsync();

        foreach (var service in legacyServices)
        {
            service.Status = "inactive";
        }
    }

    private static async Task FixDuplicatePackagesAsync(MomCareContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE services SET status = 'inactive' WHERE id BETWEEN 64 AND 72");

        await FixPackageNamesAsync(context);
        await context.SaveChangesAsync();
    }

    private static async Task FixPackageNamesAsync(MomCareContext context)
    {
        for (int id = 38; id <= 46; id++)
        {
            var service = await context.Services.FindAsync(id);
            if (service == null) continue;

            service.Name = id switch
            {
                38 => "Gói Dùng Thử Trải Nghiệm Chăm Sóc Sau Sinh Tại Nhà",
                39 => "Gói Hỗ Trợ Tuyến Sữa & Xử Lý Tắc Tia Sữa Sau Sinh",
                40 => "Gói Massage Giảm Nhức Mỏi & Phục Hồi Cơ Thể Sau Sinh",
                41 => "Gói Chăm Sóc Toàn Diện Trẻ Sơ Sinh Tại Nhà",
                42 => "Gói Massage Phục Hồi Mẹ & Tắm Bé Kết Hợp",
                43 => "Gói Phục Hồi Sức Khỏe & Tinh Thần Sau Sinh Toàn Diện",
                44 => "Gói VIP Chăm Sóc Toàn Diện Mẹ & Bé Sau Sinh",
                45 => "Gói Chăm Sóc Chuyên Sâu Mẹ & Bé Toàn Diện",
                46 => "Gói Phục Hồi Vóc Dáng & Định Hình Cơ Thể Sau Sinh",
                _ => service.Name
            };
        }
    }

    private static async Task<NurseProfile> EnsureNurseProfileAsync(
        MomCareContext context,
        int userId,
        string bio,
        string specialization,
        string certificates,
        int yearsExperience,
        int serviceRadiusKm,
        decimal averageRating,
        bool isActive,
        string verifyStatus)
    {
        var profile = await context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == userId);
        if (profile == null)
        {
            profile = new NurseProfile
            {
                UserId = userId,
                Bio = bio,
                Specialization = specialization,
                Certificates = certificates,
                YearsExperience = yearsExperience,
                ServiceRadiusKm = serviceRadiusKm,
                AverageRating = averageRating,
                IsActive = isActive,
                IsVerified = verifyStatus,
                VerificationSubmissionStatus = verifyStatus == "verified" ? "approved" : "draft",
                ConfirmedAt = verifyStatus == "verified" ? DateTime.UtcNow.AddDays(-7) : null
            };

            context.NurseProfiles.Add(profile);
            return profile;
        }

        profile.Bio = bio;
        profile.Specialization = specialization;
        profile.Certificates = certificates;
        profile.YearsExperience = yearsExperience;
        profile.ServiceRadiusKm = serviceRadiusKm;
        profile.AverageRating = averageRating;
        profile.IsActive = isActive;
        profile.IsVerified = verifyStatus;
        profile.VerificationSubmissionStatus = verifyStatus == "verified" ? "approved" : profile.VerificationSubmissionStatus;
        profile.ConfirmedAt = verifyStatus == "verified" ? profile.ConfirmedAt ?? DateTime.UtcNow.AddDays(-7) : null;

        return profile;
    }

    private static async Task EnsureDocumentAsync(
        MomCareContext context,
        int nurseProfileId,
        string type,
        string publicId,
        string status)
    {
        var normalizedType = NormalizeLegacyDocumentType(type);
        var document = await context.Documents.FirstOrDefaultAsync(d => d.NurseProfileId == nurseProfileId && d.Type == normalizedType);
        if (document == null)
        {
            context.Documents.Add(new Document
            {
                NurseProfileId = nurseProfileId,
                Type = normalizedType,
                PublicId = publicId,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        document.Type = normalizedType;
        document.PublicId = publicId;
        document.Status = status;
        document.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeLegacyDocumentType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "id_card" => DocumentTypes.IdCardFront,
            "hospital_certificate" => DocumentTypes.Certificate,
            _ => normalized
        };
    }

    private static async Task EnsureNurseServiceAsync(
        MomCareContext context,
        int nurseProfileId,
        int serviceId,
        decimal price,
        string unit,
        string status)
    {
        var nurseService = await context.NurseServices
            .FirstOrDefaultAsync(ns => ns.NurseProfileId == nurseProfileId && ns.ServiceId == serviceId);

        if (nurseService == null)
        {
            context.NurseServices.Add(new NurseService
            {
                NurseProfileId = nurseProfileId,
                ServiceId = serviceId,
                Price = price,
                Unit = unit,
                Status = status
            });
            return;
        }

        nurseService.Price = price;
        nurseService.Unit = unit;
        nurseService.Status = status;
    }

    private static async Task EnsureAvailabilitySlotAsync(
        MomCareContext context,
        int nurseProfileId,
        DateTime start,
        DateTime end)
    {
        var slot = await context.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.NurseProfileId == nurseProfileId && s.StartTime == start && s.EndTime == end);

        if (slot == null)
        {
            context.AvailabilitySlots.Add(new AvailabilitySlot
            {
                NurseProfileId = nurseProfileId,
                StartTime = start,
                EndTime = end
            });
            return;
        }
    }

    private static async Task<Booking> EnsureBookingAsync(
        MomCareContext context,
        string seedKey,
        int customerId,
        int nurseId,
        int serviceId,
        string status,
        decimal totalPrice,
        string address,
        DateTime start,
        DateTime end)
    {
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Notes == seedKey);
        if (booking == null)
        {
            booking = new Booking
            {
                CustomerId = customerId,
                NurseId = nurseId,
                ServiceId = serviceId,
                Status = status,
                TotalPrice = totalPrice,
                Notes = seedKey,
                Address = address,
                StartTime = start,
                EndTime = end,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Bookings.Add(booking);
            return booking;
        }

        booking.CustomerId = customerId;
        booking.NurseId = nurseId;
        booking.ServiceId = serviceId;
        booking.Status = status;
        booking.TotalPrice = totalPrice;
        booking.Address = address;
        booking.StartTime = start;
        booking.EndTime = end;
        booking.UpdatedAt = DateTime.UtcNow;

        return booking;
    }

    private static async Task EnsureBookingStatusHistoryAsync(
        MomCareContext context,
        int bookingId,
        string status,
        int? changedBy,
        string note,
        DateTime createdAt)
    {
        var exists = await context.BookingStatusHistories
            .AnyAsync(h => h.BookingId == bookingId && h.Status == status && h.Note == note);

        if (exists)
        {
            return;
        }

        context.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = bookingId,
            Status = status,
            ChangedBy = changedBy,
            Note = note,
            CreatedAt = createdAt
        });
    }

    private static async Task EnsurePaymentAsync(
        MomCareContext context,
        int bookingId,
        decimal amount,
        string method,
        string status,
        string transactionId)
    {
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment == null)
        {
            context.Payments.Add(new Payment
            {
                BookingId = bookingId,
                Amount = amount,
                Method = method,
                Status = status,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        payment.Amount = amount;
        payment.Method = method;
        payment.Status = status;
        payment.TransactionId = transactionId;
    }

    private static async Task EnsurePayoutAsync(
        MomCareContext context,
        int bookingId,
        int nurseId,
        decimal amount,
        decimal platformFee,
        string status,
        DateTime? releasedAt)
    {
        var payout = await context.Payouts.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payout == null)
        {
            context.Payouts.Add(new Payout
            {
                BookingId = bookingId,
                NurseId = nurseId,
                Amount = amount,
                PlatformFee = platformFee,
                Status = status,
                ReleasedAt = releasedAt,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        payout.NurseId = nurseId;
        payout.Amount = amount;
        payout.PlatformFee = platformFee;
        payout.Status = status;
        payout.ReleasedAt = releasedAt;
    }

    private static async Task EnsureReviewAsync(
        MomCareContext context,
        int bookingId,
        int customerId,
        int nurseId,
        int rating,
        string comment,
        DateTime createdAt)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
        if (review == null)
        {
            context.Reviews.Add(new Review
            {
                BookingId = bookingId,
                CustomerId = customerId,
                NurseId = nurseId,
                Rating = rating,
                Comment = comment,
                CreatedAt = createdAt
            });
            return;
        }

        review.CustomerId = customerId;
        review.NurseId = nurseId;
        review.Rating = rating;
        review.Comment = comment;
    }

    private static async Task EnsureDisputeAsync(
        MomCareContext context,
        int bookingId,
        string reason,
        string status,
        string? adminNote)
    {
        var dispute = await context.Disputes.FirstOrDefaultAsync(d => d.BookingId == bookingId);
        if (dispute == null)
        {
            context.Disputes.Add(new Dispute
            {
                BookingId = bookingId,
                Reason = reason,
                Status = status,
                AdminNote = adminNote,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        dispute.Reason = reason;
        dispute.Status = status;
        dispute.AdminNote = adminNote;
    }

    private static async Task<Conversation> EnsureConversationAsync(
        MomCareContext context,
        int bookingId,
        int user1Id,
        int user2Id)
    {
        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.BookingId == bookingId);
        if (conversation == null)
        {
            conversation = new Conversation
            {
                BookingId = bookingId,
                User1Id = user1Id,
                User2Id = user2Id,
                CreatedAt = DateTime.UtcNow
            };

            context.Conversations.Add(conversation);
            return conversation;
        }

        conversation.User1Id = user1Id;
        conversation.User2Id = user2Id;
        return conversation;
    }

    private static async Task EnsureChatMessageAsync(
        MomCareContext context,
        int conversationId,
        int senderId,
        string content,
        bool isRead,
        DateTime createdAt)
    {
        var exists = await context.ChatMessages
            .AnyAsync(m => m.ConversationId == conversationId && m.SenderId == senderId && m.Content == content);

        if (exists)
        {
            return;
        }

        context.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            IsRead = isRead,
            CreatedAt = createdAt
        });
    }

    private static async Task EnsureNotificationAsync(
        MomCareContext context,
        int userId,
        string title,
        string content,
        string type)
    {
        var exists = await context.Notifications
            .AnyAsync(n => n.UserId == userId && n.Title == title && n.Content == content);

        if (exists)
        {
            return;
        }

        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }
}
