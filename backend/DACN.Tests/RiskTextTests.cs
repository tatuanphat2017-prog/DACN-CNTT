using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;

namespace DACN.Tests;

public class RiskTextTests
{
    private static readonly MlFeatures Student = new(1, 0.25, 0.10, 3.5, null, 2.5);

    [Theory]
    [InlineData("absent_rate", "Tỷ lệ vắng 25%")]
    [InlineData("late_rate", "Tỷ lệ đi trễ 10%")]
    [InlineData("assignment_score", "Điểm bài tập 3.5")]
    [InlineData("prior_gpa", "GPA tích lũy 2.5")]
    public void Label_ShowsTheStudentsRealValue(string feature, string expected) =>
        Assert.Equal(expected, RiskText.Label(feature, Student));

    [Fact]
    public void Label_WithoutValue_FallsBackToTheName() =>
        Assert.Equal("Điểm giữa kỳ", RiskText.Label("midterm_score", Student));

    [Fact]
    public void Suggestions_ForLowRisk_AreToKeepGoing() =>
        Assert.Equal(["Tiếp tục duy trì kết quả hiện tại"], RiskText.Suggestions(RiskLevel.Low, [new RiskFactor("absent_rate", "x", 0.1, "increase")]));

    [Fact]
    public void Suggestions_ForHighRisk_AreBasedOnIncreasingFactorsAndEndWithSeeingTheLecturer()
    {
        var factors = new[]
        {
            new RiskFactor("absent_rate", "Tỷ lệ vắng 40%", 0.3, "increase"),
            new RiskFactor("prior_gpa", "GPA tích lũy 3.8", 0.1, "decrease"),     // làm giảm nguy cơ: không đưa vào gợi ý
            new RiskFactor("midterm_score", "Điểm giữa kỳ 3.0", 0.2, "increase"),
        };
        var suggestions = RiskText.Suggestions(RiskLevel.High, factors);

        Assert.Equal(3, suggestions.Count);
        Assert.Contains("Tham gia đầy đủ các buổi học còn lại", suggestions);
        Assert.Contains("Ôn lại các phần kiến thức giữa kỳ làm chưa tốt", suggestions);
        Assert.DoesNotContain(suggestions, s => s.Contains("cố vấn"));
        Assert.Equal("Gặp giảng viên để được hỗ trợ sớm", suggestions[^1]);
    }

    [Fact]
    public void Suggestions_ForMediumRisk_SuggestTalkingToTheLecturerIfNeeded() =>
        Assert.Equal("Trao đổi với giảng viên nếu gặp khó khăn", RiskText.Suggestions(RiskLevel.Medium, []).Last());

    [Fact]
    public void AutoWarning_MentionsCourseProbabilityAndMainCauses()
    {
        var text = RiskText.AutoWarning("Cơ sở dữ liệu", 0.876,
            [new RiskFactor("absent_rate", "Tỷ lệ vắng 40%", 0.3, "increase"), new RiskFactor("prior_gpa", "GPA tích lũy 3.8", 0.1, "decrease")]);

        Assert.Contains("Cơ sở dữ liệu", text);
        Assert.Contains("mức Cao", text);
        Assert.Contains("88%", text);
        Assert.Contains("Tỷ lệ vắng 40%", text);
        Assert.DoesNotContain("GPA tích lũy 3.8", text);
        Assert.True(text.Length <= 1000, "phải vừa cột Message (1000 ký tự)");
    }
}
