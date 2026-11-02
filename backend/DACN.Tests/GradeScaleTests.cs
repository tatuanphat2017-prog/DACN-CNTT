using DACN.Api.Services;

namespace DACN.Tests;

public class GradeScaleTests
{
    [Theory]
    [InlineData(10, 4.0)]
    [InlineData(8.5, 4.0)]
    [InlineData(8.4, 3.0)]
    [InlineData(7.0, 3.0)]
    [InlineData(6.9, 2.0)]
    [InlineData(5.5, 2.0)]
    [InlineData(5.4, 1.0)]
    [InlineData(4.0, 1.0)]
    [InlineData(3.9, 0.0)]
    [InlineData(0, 0.0)]
    public void ToGradePoint_FollowsTheScale(double score, double expected) =>
        Assert.Equal((decimal)expected, GradeScale.ToGradePoint((decimal)score));

    [Fact]
    public void Gpa_WithoutResults_IsNull() =>
        Assert.Null(GradeScale.Gpa([]));

    [Fact]
    public void Gpa_IsWeightedByCredits()
    {
        // (4.0 × 3 + 2.0 × 4) / 7 = 20 / 7 = 2.857... → 2.86
        var gpa = GradeScale.Gpa([(9.0m, true, 3), (6.0m, true, 4)]);
        Assert.Equal(2.86m, gpa);
    }

    [Fact]
    public void Gpa_FailedCourseCountsAsZeroEvenWithGoodScore()
    {
        // Rớt vì vắng quá nhiều: điểm 9.0 nhưng IsPassed = false → 0 điểm
        var gpa = GradeScale.Gpa([(9.0m, false, 3), (9.0m, true, 3)]);
        Assert.Equal(2.0m, gpa);
    }

    [Fact]
    public void Gpa_StaysWithinZeroToFour()
    {
        Assert.Equal(4.0m, GradeScale.Gpa([(10m, true, 3), (9m, true, 4)]));
        Assert.Equal(0m, GradeScale.Gpa([(1m, false, 3)]));
    }
}
