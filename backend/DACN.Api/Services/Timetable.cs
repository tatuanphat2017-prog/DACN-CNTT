namespace DACN.Api.Services;

/// <summary>Quy ước tiết học và năm học. Tiết 1 bắt đầu 07:00, mỗi tiết 45 phút, nghỉ 5 phút giữa các tiết (tối đa 12 tiết/ngày).</summary>
public static class Timetable
{
    public const int PeriodsPerDay = 12;
    public const int PeriodMinutes = 45;
    public const int BreakMinutes = 5;

    public static TimeOnly PeriodStart(int period) =>
        new TimeOnly(7, 0).AddMinutes((period - 1) * (PeriodMinutes + BreakMinutes));

    public static TimeOnly PeriodEnd(int period) => PeriodStart(period).AddMinutes(PeriodMinutes);

    /// <summary>Giờ bắt đầu - kết thúc của một ca gồm <paramref name="count"/> tiết liên tiếp từ <paramref name="start"/>.</summary>
    public static (TimeOnly Start, TimeOnly End) Range(int start, int count) => (PeriodStart(start), PeriodEnd(start + count - 1));

    /// <summary>Thứ theo kiểu Việt Nam: 2 = Thứ hai ... 7 = Thứ bảy, 8 = Chủ nhật.</summary>
    public static int Weekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;

    /// <summary>Năm học của một học kỳ theo ngày bắt đầu: từ tháng 7 trở đi thuộc năm học "Y-(Y+1)", trước đó thuộc "(Y-1)-Y".</summary>
    public static string AcademicYear(DateOnly semesterStart) =>
        semesterStart.Month >= 7 ? $"{semesterStart.Year}-{semesterStart.Year + 1}" : $"{semesterStart.Year - 1}-{semesterStart.Year}";
}
