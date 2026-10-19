using System.Globalization;
using DACN.Api.Dtos;
using DACN.Api.Entities;

namespace DACN.Api.Mock;

/// <summary>
/// Dữ liệu mẫu cố định cho các API đang ở dạng MOCK. Lớp IT001.01 có 8 sinh viên với đủ 3 mức rủi ro
/// (4 Low, 2 Medium, 2 High) để Frontend dựng dashboard. Xóa dần khi viết logic thật.
/// </summary>
public static class MockData
{
    private const int SessionsHeld = 6;   // số buổi đã diễn ra trong dữ liệu mẫu
    private static DateTime PredictedAt => DateTime.UtcNow.Date.AddHours(1);

    public static readonly DepartmentDto[] Departments =
    [
        new(1, "CNTT", "Công nghệ thông tin"),
        new(2, "KT", "Kinh tế"),
    ];

    public static readonly CourseDto[] Courses =
    [
        new(1, "IT001", "Nhập môn lập trình", 3, 1, "Công nghệ thông tin"),
        new(2, "IT002", "Cấu trúc dữ liệu và giải thuật", 4, 1, "Công nghệ thông tin"),
        new(3, "IT003", "Cơ sở dữ liệu", 3, 1, "Công nghệ thông tin"),
    ];

    public static readonly SemesterDto[] Semesters =
    [
        new(1, "HK2 2025-2026", new DateOnly(2026, 2, 15), new DateOnly(2026, 6, 30), false),
        new(2, "HK1 2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15), true),
    ];

    public static readonly SectionWeights Weights = new(0.10m, 0.20m, 0.20m, 0.50m);

    public static readonly LecturerItem[] Lecturers =
    [
        new(1, "GV001", "TS. Nguyễn Thị Lan", "Công nghệ thông tin"),
        new(2, "GV002", "ThS. Trần Văn Minh", "Công nghệ thông tin"),
    ];

    public static readonly SectionListItem[] Sections =
    [
        new(1, "IT001.01", 1, "IT001", "Nhập môn lập trình", 2, "HK1 2026-2027", 1, "TS. Nguyễn Thị Lan", 15, 8),
        new(2, "IT002.01", 2, "IT002", "Cấu trúc dữ liệu và giải thuật", 2, "HK1 2026-2027", 1, "TS. Nguyễn Thị Lan", 15, 8),
        new(3, "IT003.01", 3, "IT003", "Cơ sở dữ liệu", 2, "HK1 2026-2027", 2, "ThS. Trần Văn Minh", 15, 8),
    ];

    private record Seed(
        int EnrollmentId, int StudentId, string Code, string Name, string ClassName,
        int Absent, decimal Gpa, decimal Att, decimal Asg, decimal Mid, double Prob);

    private static readonly Seed[] Seeds =
    [
        new(101, 1, "SV001", "Nguyễn Văn An", "CNTT2025A", 0, 3.4m, 10.0m, 8.5m, 8.0m, 0.08),
        new(102, 2, "SV002", "Trần Thị Bình", "CNTT2025A", 2, 2.6m, 8.7m, 6.5m, 5.5m, 0.38),
        new(103, 3, "SV003", "Lê Hoàng Cường", "CNTT2025A", 6, 1.8m, 6.0m, 3.0m, 3.5m, 0.82),
        new(104, 4, "SV004", "Phạm Minh Đức", "CNTT2025A", 1, 3.0m, 9.3m, 7.5m, 7.0m, 0.15),
        new(105, 5, "SV005", "Võ Thị Em", "CNTT2025B", 0, 3.7m, 10.0m, 9.0m, 9.0m, 0.05),
        new(106, 6, "SV006", "Đặng Quốc Phong", "CNTT2025B", 5, 2.0m, 6.7m, 4.0m, 4.0m, 0.71),
        new(107, 7, "SV007", "Bùi Thị Giang", "CNTT2025B", 3, 2.4m, 8.0m, 5.5m, 5.0m, 0.47),
        new(108, 8, "SV008", "Hoàng Văn Hải", "CNTT2025B", 1, 2.9m, 9.3m, 7.0m, 6.5m, 0.22),
    ];

    // Sinh viên đang đăng nhập trong các API /api/students/me/* của dữ liệu mẫu
    private static readonly Seed Me = Seeds[1];

    public static readonly UserListItem[] Users = BuildUsers();

    private static UserListItem[] BuildUsers()
    {
        var created = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var list = new List<UserListItem> { new(1, "admin", "Quản trị viên", null, UserRole.Admin, true, created) };
        list.AddRange(Lecturers.Select((l, i) =>
            new UserListItem(2 + i, l.LecturerCode, l.FullName, $"{l.LecturerCode.ToLowerInvariant()}@school.edu.vn", UserRole.Lecturer, true, created)));
        list.AddRange(Seeds.Select((s, i) =>
            new UserListItem(4 + i, s.Code, s.Name, $"{s.Code.ToLowerInvariant()}@student.school.edu.vn", UserRole.Student, true, created)));
        return [.. list];
    }

    public static UserDetail? UserDetail(int id)
    {
        var u = Users.FirstOrDefault(x => x.Id == id);
        if (u is null) return null;
        StudentProfile? student = u.Role == UserRole.Student
            ? new StudentProfile(u.Username, 1, Seeds.First(s => s.Code == u.Username).ClassName, 2025) : null;
        LecturerProfile? lecturer = u.Role == UserRole.Lecturer ? new LecturerProfile(u.Username, 1) : null;
        return new UserDetail(u.Id, u.Username, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt, student, lecturer);
    }

    public static StudentItem[] StudentItems =>
        [.. Seeds.Select(s => new StudentItem(s.StudentId, s.Code, s.Name, s.ClassName, "Công nghệ thông tin"))];

    public static SectionListItem? Section(int id) => Sections.FirstOrDefault(s => s.Id == id);

    public static SectionDetail SectionDetail(SectionListItem s) =>
        new(s.Id, s.Code, s.CourseId, s.CourseCode, s.CourseName, s.SemesterId, s.SemesterName,
            s.LecturerId, s.LecturerName, s.TotalSessions, Weights, s.StudentCount);

    public static List<SectionStudent> SectionStudents() =>
        [.. Seeds.Select(s => new SectionStudent(s.EnrollmentId, s.StudentId, s.Code, s.Name, s.ClassName))];

    public static GradeSheet Grades(int sectionId) =>
        new(sectionId, Weights, 15,
            [.. Seeds.Select(s => new GradeRow(s.EnrollmentId, s.StudentId, s.Code, s.Name, s.Absent, s.Gpa,
                s.Att, s.Asg, s.Mid, null, null, null))]);

    // ----- Điểm danh -----

    private static bool IsAbsent(Seed s, int sessionNo) =>
        s.Absent > 0 && Enumerable.Range(0, s.Absent).Any(j => j * SessionsHeld / s.Absent + 1 == sessionNo);

    private static AttendanceStatus StatusOf(Seed s, int sessionNo)
    {
        if (IsAbsent(s, sessionNo)) return AttendanceStatus.Absent;
        if (s.StudentId == 4 && sessionNo == 3) return AttendanceStatus.Late;
        if (s.StudentId == 2 && sessionNo == 6) return AttendanceStatus.Excused;
        return AttendanceStatus.Present;
    }

    private static DateOnly SessionDate(int sessionNo) => new DateOnly(2026, 9, 7).AddDays(7 * (sessionNo - 1));

    public static List<AttendanceSessionSummary> AttendanceSessions() =>
    [
        .. Enumerable.Range(1, 15).Select(n =>
        {
            if (n > SessionsHeld) return new AttendanceSessionSummary(n, null, 0, 0, 0, 0);
            var statuses = Seeds.Select(s => StatusOf(s, n)).ToList();
            return new AttendanceSessionSummary(n, SessionDate(n),
                statuses.Count(x => x == AttendanceStatus.Present), statuses.Count(x => x == AttendanceStatus.Absent),
                statuses.Count(x => x == AttendanceStatus.Late), statuses.Count(x => x == AttendanceStatus.Excused));
        }),
    ];

    public static AttendanceSheet AttendanceSheet(int sectionId, int sessionNo) =>
        new(sectionId, sessionNo, SessionDate(sessionNo),
            [.. Seeds.Select(s => new AttendanceEntry(s.EnrollmentId, s.StudentId, s.Code, s.Name,
                sessionNo <= SessionsHeld ? StatusOf(s, sessionNo) : AttendanceStatus.Present))]);

    // ----- Rủi ro -----

    private static RiskLevel Level(double p) => p < 0.3 ? RiskLevel.Low : p < 0.6 ? RiskLevel.Medium : RiskLevel.High;

    private static string Text(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static List<RiskFactor> Factors(Seed s)
    {
        var absentRate = s.Absent / 15.0;
        return
        [
            .. new[]
            {
                new RiskFactor("absent_rate", Text($"Vắng {s.Absent}/15 buổi ({absentRate * 100:0}%)"),
                    Math.Round(absentRate, 2), absentRate > 0.15 ? "increase" : "decrease"),
                new RiskFactor("midterm_score", Text($"Điểm giữa kỳ {s.Mid:0.0}"),
                    Math.Round((10 - (double)s.Mid) / 20, 2), s.Mid < 5 ? "increase" : "decrease"),
                new RiskFactor("prior_gpa", Text($"GPA tích lũy {s.Gpa:0.0}"),
                    Math.Round((4 - (double)s.Gpa) / 12, 2), s.Gpa < 2.5m ? "increase" : "decrease"),
            }.OrderByDescending(f => f.Impact),
        ];
    }

    public static List<RiskItem> Risks() =>
        [.. Seeds.Select(s => new RiskItem(s.EnrollmentId, s.StudentId, s.Code, s.Name,
            s.Prob, Level(s.Prob), Factors(s), PredictedAt))];

    public static RiskSummary RiskSummary(int sectionId)
    {
        var risks = Risks();
        return new RiskSummary(sectionId, risks.Count,
            risks.Count(r => r.RiskLevel == RiskLevel.Low), risks.Count(r => r.RiskLevel == RiskLevel.Medium),
            risks.Count(r => r.RiskLevel == RiskLevel.High), PredictedAt, "v1.0-randomforest");
    }

    public static List<RiskHistoryPoint> RiskHistory(int enrollmentId)
    {
        var seed = Seeds.FirstOrDefault(s => s.EnrollmentId == enrollmentId) ?? Seeds[0];
        return
        [
            .. new[] { -0.15, -0.08, 0.0 }.Select((delta, i) =>
            {
                var p = Math.Clamp(Math.Round(seed.Prob + delta, 2), 0, 1);
                return new RiskHistoryPoint(PredictedAt.AddDays(-14 * (2 - i)), p, Level(p));
            }),
        ];
    }

    // ----- Cảnh báo -----

    public static List<WarningItem> Warnings(bool forMe) =>
    [
        new(1, 102, 1, "Nhập môn lập trình", "Trần Thị Bình", "TS. Nguyễn Thị Lan",
            "Em đã vắng 2 buổi và điểm giữa kỳ còn thấp, hãy gặp cô sau giờ học để được hỗ trợ.", false, PredictedAt.AddDays(-2)),
        new(2, 102, 1, "Nhập môn lập trình", "Trần Thị Bình", null,
            "Hệ thống phát hiện nguy cơ rớt môn ở mức Trung bình. Hãy tham gia đầy đủ các buổi còn lại.", true, PredictedAt.AddDays(-9)),
    ];

    // ----- Cổng sinh viên (/api/students/me) -----

    public static List<MySectionItem> MySections() =>
    [
        new(1, "IT001.01", "Nhập môn lập trình", "HK1 2026-2027", "TS. Nguyễn Thị Lan", Me.Absent, 15, null, RiskLevel.Medium),
        new(2, "IT002.01", "Cấu trúc dữ liệu và giải thuật", "HK1 2026-2027", "TS. Nguyễn Thị Lan", 1, 15, null, RiskLevel.Low),
        new(3, "IT003.01", "Cơ sở dữ liệu", "HK1 2026-2027", "ThS. Trần Văn Minh", 0, 15, null, RiskLevel.Low),
    ];

    public static MySectionDetail? MySection(int sectionId)
    {
        var section = MySections().FirstOrDefault(s => s.SectionId == sectionId);
        if (section is null) return null;
        var attendance = Enumerable.Range(1, SessionsHeld)
            .Select(n => new MyAttendanceRecord(n, SessionDate(n), StatusOf(Me, n))).ToList();
        return new MySectionDetail(section, Weights, Me.Att, Me.Asg, Me.Mid, null, null, null, attendance);
    }

    public static List<MyRisk> MyRisks() =>
    [
        new(1, "IT001.01", "Nhập môn lập trình", 0.38, RiskLevel.Medium, Factors(Me),
            ["Tham gia đầy đủ các buổi học còn lại", "Ôn lại các bài kiểm tra giữa kỳ làm chưa tốt", "Gặp giảng viên để được hỗ trợ thêm"], PredictedAt),
        new(2, "IT002.01", "Cấu trúc dữ liệu và giải thuật", 0.14, RiskLevel.Low, Factors(Seeds[3]),
            ["Tiếp tục duy trì kết quả hiện tại"], PredictedAt),
        new(3, "IT003.01", "Cơ sở dữ liệu", 0.06, RiskLevel.Low, Factors(Seeds[4]),
            ["Tiếp tục duy trì kết quả hiện tại"], PredictedAt),
    ];

    // ----- ML -----

    public static ModelVersionItem[] Models =
    [
        new(1, "v1.0-randomforest", "RandomForest", 0.86, 0.84, 0.81, 0.82, 0.91, new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), true),
        new(2, "v0.9-logreg", "LogisticRegression", 0.80, 0.77, 0.74, 0.75, 0.85, new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc), false),
    ];

    // ----- Phân trang -----

    public static PagedResult<T> Page<T>(IEnumerable<T> source, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var all = source.ToList();
        return new PagedResult<T>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);
    }
}
