namespace MainQuestline.Reference;

/// <summary>
/// Every player-facing string of the Main Quest tab and banners, in Vietnamese (operator decision 2026-10-05).
/// This is the single source for the mod's UI string table. In-game proper names (boss nameplates, map places)
/// stay as the game shows them; admin/QA output and logs stay English.
/// </summary>
public static class MainQuestText
{
    // ---- tab chrome
    public const string TabTitle = "NHIỆM VỤ CHÍNH";
    public const string CampaignProgress = "TIẾN ĐỘ CHIẾN DỊCH";
    public const string QuestsCompleteFormat = "Đã hoàn thành {0}/{1} nhiệm vụ chính";
    public const string CurrentQuest = "NHIỆM VỤ HIỆN TẠI";
    public const string Quest = "NHIỆM VỤ";
    public const string Boss = "Trùm";
    public const string Objective = "Mục tiêu";
    public const string Location = "Địa điểm";
    public const string RecommendedLevel = "Cấp đề xuất";
    public const string RecommendedParty = "Nhóm đề xuất";
    public const string Reward = "Phần thưởng";
    public const string RewardPlaceholder = "tạm thời";
    public const string MapMarker = "ĐIỂM ĐÁNH DẤU";
    public const string TrackQuest = "THEO DÕI NHIỆM VỤ";
    public const string Tracking = "ĐANG THEO DÕI";
    public const string HistoryTitle = "ĐÃ HOÀN THÀNH · LỊCH SỬ";
    public const string Retired = "đã gỡ khỏi chiến dịch";
    public const string StagingUnverified = "STAGING · MỤC TIÊU CHƯA XÁC MINH · CHƯA GHI NHẬN";
    public const string CampaignCompleteTitle = "CHIẾN DỊCH HOÀN TẤT";

    // ---- states
    public const string StatusActive = "ĐANG LÀM";
    public const string StatusCompleted = "ĐÃ HOÀN THÀNH";
    public const string StatusLocked = "CHƯA MỞ KHÓA";

    public static string Status(QuestStatus s) => s switch
    {
        QuestStatus.Active => StatusActive,
        QuestStatus.Completed => StatusCompleted,
        _ => StatusLocked
    };

    // ---- notes and hints
    public const string LockedHint = "Hoàn thành nhiệm vụ chính trước đó để mở khóa mục tiêu này.";
    public const string ReachLevelFormat = "Đạt cấp {0} để bắt đầu.";
    public const string DefeatFormat = "Hạ gục {0}.";

    // ---- marker (why it is hidden)
    public const string MarkerNoActiveQuest = "Không có nhiệm vụ chính đang làm";
    public const string MarkerNotTracked = "Chưa theo dõi nhiệm vụ";
    public const string MarkerRequiresLevelFormat = "Cần đạt cấp {0}";
    public const string MarkerPreviousNotDone = "Chưa hoàn thành nhiệm vụ trước";
    public const string MarkerNone = "Nhiệm vụ này chưa có điểm đánh dấu trên bản đồ";

    // ---- banners
    public const string BannerQuestComplete = "HOÀN THÀNH NHIỆM VỤ";
    public const string BannerNewBoss = "MỞ KHÓA TRÙM MỚI";
    public const string BannerMilestone = "CỘT MỐC CHIẾN DỊCH";
    public const string ActCompleteFormat = "Hoàn thành {0}";
    public const string CampaignComplete = "Hoàn thành toàn bộ chiến dịch";

    // ---- rewards
    public const string XpFormat = "{0} XP";
}
