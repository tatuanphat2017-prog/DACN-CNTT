using System.Globalization;
using DACN.Api.Dtos;
using DACN.Api.Entities;

namespace DACN.Api.Services;

/// <summary>Nhãn tiếng Việt cho các yếu tố rủi ro, gợi ý cải thiện cho sinh viên và nội dung cảnh báo tự động.</summary>
public static class RiskText
{
    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>Mô tả ngắn một yếu tố kèm giá trị thật của sinh viên, ví dụ "Tỷ lệ vắng 25%" hoặc "Điểm giữa kỳ 3.5".</summary>
    public static string Label(string feature, MlFeatures f) => feature switch
    {
        "absent_rate" => F($"Tỷ lệ vắng {f.AbsentRate * 100:0}%"),
        "late_rate" => F($"Tỷ lệ đi trễ {f.LateRate * 100:0}%"),
        "assignment_score" => f.AssignmentScore is { } a ? F($"Điểm bài tập {a:0.0}") : "Điểm bài tập",
        "midterm_score" => f.MidtermScore is { } m ? F($"Điểm giữa kỳ {m:0.0}") : "Điểm giữa kỳ",
        "prior_gpa" => f.PriorGpa is { } g ? F($"GPA tích lũy {g:0.0#}") : "GPA tích lũy",
        _ => feature,
    };

    /// <summary>Gợi ý hành động cho từng yếu tố đang làm tăng nguy cơ.</summary>
    private static string? Suggestion(string feature) => feature switch
    {
        "absent_rate" => "Tham gia đầy đủ các buổi học còn lại",
        "late_rate" => "Đến lớp đúng giờ",
        "assignment_score" => "Hoàn thành và nộp đầy đủ các bài tập còn lại",
        "midterm_score" => "Ôn lại các phần kiến thức giữa kỳ làm chưa tốt",
        "prior_gpa" => "Trao đổi với cố vấn học tập để có kế hoạch học tập phù hợp",
        _ => null,
    };

    public static List<string> Suggestions(RiskLevel level, IEnumerable<RiskFactor> factors)
    {
        if (level == RiskLevel.Low)
        {
            return ["Tiếp tục duy trì kết quả hiện tại"];
        }

        var list = factors.Where(x => x.Direction == "increase").Select(x => Suggestion(x.Feature))
            .Where(s => s is not null).Select(s => s!).Distinct().Take(3).ToList();
        list.Add(level == RiskLevel.High ? "Gặp giảng viên để được hỗ trợ sớm" : "Trao đổi với giảng viên nếu gặp khó khăn");
        return list;
    }

    /// <summary>Nội dung cảnh báo hệ thống tự gửi khi sinh viên ở mức rủi ro Cao.</summary>
    public static string AutoWarning(string courseName, double probability, IEnumerable<RiskFactor> factors)
    {
        var causes = string.Join("; ", factors.Where(x => x.Direction == "increase").Take(3).Select(x => x.Label));
        var reason = causes.Length > 0 ? $" Nguyên nhân chính: {causes}." : "";
        return F($"Hệ thống phát hiện nguy cơ rớt môn {courseName} ở mức Cao (xác suất {probability * 100:0}%).{reason} Hãy gặp giảng viên để được hỗ trợ sớm.");
    }
}
