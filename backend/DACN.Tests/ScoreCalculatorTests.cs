using DACN.Api.Dtos;
using DACN.Api.Services;

namespace DACN.Tests;

public class ScoreCalculatorTests
{
    private static readonly SectionWeights Weights = new(0.10m, 0.20m, 0.20m, 0.50m);

    // ----- Điểm chuyên cần -----

    [Fact]
    public void AttendanceScore_WithoutAnySession_IsNull() =>
        Assert.Null(ScoreCalculator.AttendanceScore(new AttendanceCounts(0, 0, 0, 0)));

    [Fact]
    public void AttendanceScore_AllPresent_IsTen() =>
        Assert.Equal(10m, ScoreCalculator.AttendanceScore(new AttendanceCounts(8, 0, 0, 0)));

    [Fact]
    public void AttendanceScore_AllAbsent_IsZero() =>
        Assert.Equal(0m, ScoreCalculator.AttendanceScore(new AttendanceCounts(0, 0, 0, 5)));

    [Fact]
    public void AttendanceScore_LateCountsHalf_ExcusedCountsFull()
    {
        // 2 có mặt + 1 trễ (0.5) + 1 có phép (1) + 1 vắng, trên 5 buổi: 10 × 3.5 / 5 = 7.0
        Assert.Equal(7.0m, ScoreCalculator.AttendanceScore(new AttendanceCounts(2, 1, 1, 1)));
    }

    [Fact]
    public void AttendanceScore_RoundsToTwoDecimals() =>
        Assert.Equal(6.67m, ScoreCalculator.AttendanceScore(new AttendanceCounts(2, 0, 0, 1)));

    // ----- Điểm tổng -----

    [Theory]
    [InlineData(null, 8.0, 7.0, 9.0)]
    [InlineData(10.0, null, 7.0, 9.0)]
    [InlineData(10.0, 8.0, null, 9.0)]
    [InlineData(10.0, 8.0, 7.0, null)]
    public void TotalScore_MissingAnyComponent_IsNull(double? attendance, double? assignment, double? midterm, double? final) =>
        Assert.Null(ScoreCalculator.TotalScore(Dec(attendance), Dec(assignment), Dec(midterm), Dec(final), Weights));

    [Fact]
    public void TotalScore_AppliesWeights() =>
        // 10×0.1 + 8×0.2 + 7×0.2 + 9×0.5 = 1 + 1.6 + 1.4 + 4.5
        Assert.Equal(8.5m, ScoreCalculator.TotalScore(10m, 8m, 7m, 9m, Weights));

    [Fact]
    public void TotalScore_RoundsHalfUp()
    {
        // Toàn bộ trọng số dồn vào điểm chuyên cần để ra đúng 7.25
        var onlyAttendance = new SectionWeights(1m, 0m, 0m, 0m);
        Assert.Equal(7.3m, ScoreCalculator.TotalScore(7.25m, 0m, 0m, 0m, onlyAttendance));
    }

    // ----- Qua / rớt -----

    [Fact]
    public void IsPassed_WithoutTotal_IsNull() =>
        Assert.Null(ScoreCalculator.IsPassed(null, 0, 15));

    [Theory]
    [InlineData(4.0, 0, 15, true)]      // đúng ngưỡng điểm
    [InlineData(3.9, 0, 15, false)]     // thiếu 0.1 điểm
    [InlineData(9.0, 3, 15, true)]      // vắng 3/15 = đúng 20%
    [InlineData(9.0, 4, 15, false)]     // vắng 4/15 > 20%: rớt dù điểm cao
    [InlineData(9.0, 2, 10, true)]      // 2/10 = đúng 20%
    [InlineData(9.0, 3, 10, false)]
    [InlineData(2.0, 5, 15, false)]
    public void IsPassed_NeedsEnoughScoreAndAttendance(double total, int absent, int sessions, bool expected) =>
        Assert.Equal(expected, ScoreCalculator.IsPassed((decimal)total, absent, sessions));

    private static decimal? Dec(double? value) => value is null ? null : (decimal)value;
}
