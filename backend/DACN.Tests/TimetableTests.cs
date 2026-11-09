using DACN.Api.Services;

namespace DACN.Tests;

public class TimetableTests
{
    [Theory]
    [InlineData(1, "07:00")]
    [InlineData(2, "07:50")]
    [InlineData(4, "09:30")]
    [InlineData(7, "12:00")]
    [InlineData(12, "16:10")]
    public void PeriodStart_AddsFiftyMinutesPerPeriod(int period, string expected) =>
        Assert.Equal(TimeOnly.Parse(expected), Timetable.PeriodStart(period));

    [Theory]
    [InlineData(1, 3, "07:00", "09:25")]
    [InlineData(4, 3, "09:30", "11:55")]
    [InlineData(10, 3, "14:30", "16:55")]
    [InlineData(1, 1, "07:00", "07:45")]
    public void Range_EndsOnTheLastPeriod(int start, int count, string from, string to) =>
        Assert.Equal((TimeOnly.Parse(from), TimeOnly.Parse(to)), Timetable.Range(start, count));

    [Theory]
    [InlineData(2026, 11, 9, 2)]     // thứ hai
    [InlineData(2026, 11, 14, 7)]    // thứ bảy
    [InlineData(2026, 11, 15, 8)]    // chủ nhật
    public void Weekday_UsesVietnameseNumbering(int year, int month, int day, int expected) =>
        Assert.Equal(expected, Timetable.Weekday(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2025, 9, 8, "2025-2026")]
    [InlineData(2026, 2, 16, "2025-2026")]
    [InlineData(2026, 6, 30, "2025-2026")]
    [InlineData(2026, 7, 1, "2026-2027")]
    [InlineData(2027, 1, 15, "2026-2027")]
    public void AcademicYear_StartsInJuly(int year, int month, int day, string expected) =>
        Assert.Equal(expected, Timetable.AcademicYear(new DateOnly(year, month, day)));
}
