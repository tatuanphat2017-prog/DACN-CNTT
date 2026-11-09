using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Data;

/// <summary>
/// Tạo dữ liệu demo thật: 6 giảng viên, 120 sinh viên, 16 môn, 12 phòng học (3 tòa x 4 lầu), 3 học kỳ (2 học kỳ đã kết thúc có đủ điểm và kết quả qua/rớt,
/// 1 học kỳ hiện tại đang học giữa kỳ). Dữ liệu sinh bằng mô phỏng cố định (seed ngẫu nhiên không đổi): mỗi sinh viên có
/// "năng lực" và "độ chăm chỉ" ẩn, từ đó ra điểm danh, điểm bài tập, giữa kỳ, cuối kỳ và kết quả. Hai học kỳ cũ chính là
/// lịch sử để huấn luyện mô hình ML (nhãn = rớt môn).
///
/// Chỉ chạy khi có cấu hình Seed:DemoPassword (mật khẩu chung của mọi tài khoản demo) và database chưa có lớp học phần nào.
/// </summary>
public static class DemoDataSeeder
{
    private const int Seed = 20261102;
    private const int StudentCount = 120;
    private const int LecturerCount = 6;
    private const int TotalSessions = 15;
    private const int SessionsHeldNow = 8;        // học kỳ hiện tại đã học 8/15 buổi (đã qua giữa kỳ)
    private const int CoursesPerSemester = 5;

    private static readonly (string Code, string Name, int Credits)[] CourseList =
    [
        ("IT001", "Nhập môn lập trình", 3), ("IT002", "Cấu trúc dữ liệu và giải thuật", 4),
        ("IT003", "Cơ sở dữ liệu", 3), ("IT004", "Lập trình hướng đối tượng", 3),
        ("IT005", "Mạng máy tính", 3), ("IT006", "Hệ điều hành", 3),
        ("IT007", "Toán rời rạc", 3), ("IT008", "Công nghệ phần mềm", 3),
        ("IT009", "Phân tích thiết kế hệ thống", 3), ("IT010", "Lập trình web", 3),
        ("IT011", "Trí tuệ nhân tạo", 3), ("IT012", "Học máy", 3),
        ("IT013", "An toàn thông tin", 3), ("IT014", "Kiến trúc máy tính", 3),
        ("IT015", "Xác suất thống kê", 3), ("IT016", "Đồ họa máy tính", 3),
    ];

    private static readonly (string Name, string Code, DateOnly Start, DateOnly End, bool Past)[] SemesterList =
    [
        ("HK1 2025-2026", "2526-1", new DateOnly(2025, 9, 8), new DateOnly(2026, 1, 16), true),
        ("HK2 2025-2026", "2526-2", new DateOnly(2026, 2, 16), new DateOnly(2026, 6, 26), true),
        ("HK1 2026-2027", "2627-1", new DateOnly(2026, 9, 7), new DateOnly(2027, 1, 15), false),
    ];

    private static readonly string[] Surnames = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý"];
    private static readonly string[] MaleMiddle = ["Văn", "Minh", "Quốc", "Hữu", "Đức", "Thanh", "Gia", "Anh"];
    private static readonly string[] FemaleMiddle = ["Thị", "Ngọc", "Thu", "Phương", "Hồng", "Khánh", "Bảo"];
    private static readonly string[] MaleNames = ["An", "Bình", "Cường", "Dũng", "Đạt", "Hải", "Hiếu", "Huy", "Khoa", "Long", "Nam", "Phát", "Phong", "Quang", "Sơn", "Tâm", "Thắng", "Tuấn", "Việt"];
    private static readonly string[] FemaleNames = ["Anh", "Châu", "Chi", "Dung", "Giang", "Hà", "Hạnh", "Hoa", "Hương", "Lan", "Linh", "Mai", "My", "Ngân", "Nhi", "Quỳnh", "Thảo", "Trang", "Uyên", "Vy"];

    private class SimStudent
    {
        public required Student Student { get; init; }
        public double Ability { get; init; }
        public double Diligence { get; init; }
        public decimal? InitialGpa { get; init; }
        public List<(decimal Total, bool Passed, int Credits)> History { get; } = [];
        public List<int> Remaining { get; } = [];
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();
        var password = services.GetRequiredService<IConfiguration>()["Seed:DemoPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }
        if (await db.CourseSections.AnyAsync())
        {
            return;
        }
        if (await db.Users.AnyAsync(u => u.Username == "GV001"))
        {
            logger.LogWarning("Đã có tài khoản demo cũ (GV001) nhưng chưa có lớp học phần. Chạy 'docker compose down -v' rồi chạy lại để tạo dữ liệu demo mới.");
            return;
        }

        var rng = new Random(Seed);
        var passwordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(new User(), password);

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "CNTT")
                         ?? new Department { Code = "CNTT", Name = "Công nghệ thông tin" };

        // ----- Giảng viên -----
        var lecturerNames = new[] { "TS. Nguyễn Thị Lan", "ThS. Trần Văn Minh", "TS. Lê Quốc Hùng", "ThS. Phạm Thu Hà", "TS. Hoàng Đức Thịnh", "ThS. Võ Ngọc Mai" };
        var lecturers = Enumerable.Range(1, LecturerCount).Select(i =>
        {
            var code = $"GV{i:000}";
            var user = new User
            {
                Username = code, FullName = lecturerNames[i - 1], Email = $"{code.ToLowerInvariant()}@school.edu.vn",
                Role = UserRole.Lecturer, PasswordHash = passwordHash,
            };
            return new Lecturer { User = user, LecturerCode = code, Department = department };
        }).ToList();

        // ----- Sinh viên -----
        var students = Enumerable.Range(1, StudentCount).Select(i =>
        {
            var code = $"SV{i:000}";
            var year = i <= 40 ? 2023 : i <= 80 ? 2024 : 2025;
            var className = i <= 100 ? $"CNTT{year}A" : $"CNTT{year}B";
            var ability = Normal(rng);
            var diligence = 0.5 * ability + Math.Sqrt(0.75) * Normal(rng);     // chăm chỉ có tương quan nhẹ với năng lực
            var user = new User
            {
                Username = code, FullName = RandomName(rng), Email = $"{code.ToLowerInvariant()}@student.school.edu.vn",
                Role = UserRole.Student, PasswordHash = passwordHash,
            };
            var student = new Student { User = user, StudentCode = code, Department = department, ClassName = className, EnrollmentYear = year };
            // Khóa 2025 là sinh viên mới (chưa có GPA); các khóa trước có GPA tích lũy từ trước khi hệ thống ra đời
            decimal? gpa0 = year < 2025 ? Math.Round((decimal)Math.Clamp(2.6 + 0.6 * ability + 0.3 * Normal(rng), 0.5, 4.0), 2) : null;
            return new SimStudent { Student = student, Ability = ability, Diligence = diligence, InitialGpa = gpa0 };
        }).ToList();

        // ----- Môn học, học kỳ -----
        var courses = CourseList.Select(c => new Course { Code = c.Code, Name = c.Name, Credits = c.Credits, Department = department }).ToList();
        var courseDifficulty = courses.Select(_ => 0.4 * Normal(rng)).ToArray();     // môn khó/dễ khác nhau
        var semesters = SemesterList.Select(s => new Semester { Name = s.Name, StartDate = s.Start, EndDate = s.End, IsCurrent = !s.Past }).ToList();

        // Phòng học: 3 tòa (A1, A2, B1) x 4 lầu, mỗi lầu một phòng
        var rooms = new[] { "A1", "A2", "B1" }
            .SelectMany(b => Enumerable.Range(1, 4).Select(f => new Room { Code = $"{b}.{f}01", Building = b, Floor = f, Capacity = 50 + 10 * f }))
            .ToList();

        var weights = new SectionWeights(0.10m, 0.20m, 0.20m, 0.50m);
        foreach (var s in students)
        {
            s.Remaining.AddRange(Enumerable.Range(0, courses.Count));
        }

        var sections = new List<CourseSection>();
        var enrollments = new List<Enrollment>();
        var sessions = new List<AttendanceSession>();
        var records = new List<AttendanceRecord>();

        for (var semIndex = 0; semIndex < semesters.Count; semIndex++)
        {
            var semester = semesters[semIndex];
            var (_, semCode, semStart, semEnd, past) = SemesterList[semIndex];
            var held = past ? TotalSessions : SessionsHeldNow;

            var semSections = courses.Select((course, i) => new CourseSection
            {
                Code = $"{course.Code}.{semCode}", Course = course, Semester = semester,
                Lecturer = lecturers[(i + semIndex) % LecturerCount], TotalSessions = TotalSessions,
                AttendanceWeight = weights.AttendanceWeight, AssignmentWeight = weights.AssignmentWeight,
                MidtermWeight = weights.MidtermWeight, FinalWeight = weights.FinalWeight,
                // Mỗi lớp một cặp (thứ, ca) riêng: thứ 2..6, ca 3 tiết (tiết 1-3, 4-6, 7-9, 10-12) nên sinh viên và giảng viên không bao giờ trùng giờ
                Weekday = 2 + i % 5, StartPeriod = 1 + i / 5 * 3, PeriodCount = 3, Room = rooms[(i * 5 + semIndex * 3) % rooms.Count],
                ExamDate = ExamDay(semEnd.AddDays(-14 + i % 10)), ExamStartTime = i % 2 == 0 ? new TimeOnly(7, 30) : new TimeOnly(13, 30),
                ExamRoom = rooms[(i * 7 + semIndex) % rooms.Count],
            }).ToList();
            sections.AddRange(semSections);

            // Các buổi học: mỗi lớp học vào một thứ riêng trong tuần
            var semSessions = new List<List<AttendanceSession>>();
            for (var i = 0; i < semSections.Count; i++)
            {
                var list = Enumerable.Range(1, held)
                    .Select(n => new AttendanceSession { CourseSection = semSections[i], SessionNo = n, Date = semStart.AddDays(7 * (n - 1) + i % 5) })
                    .ToList();
                semSessions.Add(list);
                sessions.AddRange(list);
            }

            var pending = new List<(SimStudent Student, Enrollment Enrollment, int CourseIndex)>();
            foreach (var sim in students)
            {
                var picks = sim.Remaining.OrderBy(_ => rng.Next()).Take(CoursesPerSemester).ToList();
                foreach (var courseIndex in picks)
                {
                    sim.Remaining.Remove(courseIndex);
                    var enrollment = new Enrollment
                    {
                        CourseSection = semSections[courseIndex], Student = sim.Student,
                        PriorGpa = semIndex == 0 ? sim.InitialGpa : GradeScale.Gpa(sim.History),
                    };
                    enrollments.Add(enrollment);
                    pending.Add((sim, enrollment, courseIndex));
                }
            }

            foreach (var (sim, enrollment, courseIndex) in pending)
            {
                Simulate(rng, sim, enrollment, courseIndex, courses[courseIndex].Credits, courseDifficulty[courseIndex],
                    semSections[courseIndex], semSessions[courseIndex], past, weights, records);
            }
        }

        db.Users.AddRange(lecturers.Select(l => l.User));
        db.Users.AddRange(students.Select(s => s.Student.User));
        db.Semesters.AddRange(semesters);
        db.Rooms.AddRange(rooms);
        db.CourseSections.AddRange(sections);
        db.Enrollments.AddRange(enrollments);
        db.AttendanceSessions.AddRange(sessions);
        db.AttendanceRecords.AddRange(records);
        await db.SaveChangesAsync();

        var past2 = enrollments.Where(e => e.IsPassed is not null).ToList();
        logger.LogInformation(
            "Đã tạo dữ liệu demo: {Lecturers} giảng viên, {Students} sinh viên, {Courses} môn, {Sections} lớp, {Enrollments} đăng ký, {Records} lượt điểm danh. " +
            "Học kỳ đã kết thúc: {Finished} kết quả, tỷ lệ rớt {FailRate:P1}",
            lecturers.Count, students.Count, courses.Count, sections.Count, enrollments.Count, records.Count,
            past2.Count, past2.Count(e => e.IsPassed == false) / (double)past2.Count);
    }

    /// <summary>Mô phỏng điểm danh và điểm của một sinh viên trong một lớp, rồi tính điểm bằng đúng công thức của hệ thống.</summary>
    private static void Simulate(Random rng, SimStudent sim, Enrollment enrollment, int courseIndex, int credits, double difficulty,
        CourseSection section, List<AttendanceSession> sessions, bool past, SectionWeights weights, List<AttendanceRecord> records)
    {
        // Điểm danh: xác suất đi học tăng theo độ chăm chỉ
        var attendProbability = Sigmoid(2.5 + 0.9 * sim.Diligence + 0.3 * sim.Ability);
        int present = 0, late = 0, excused = 0, absent = 0;
        foreach (var session in sessions)
        {
            AttendanceStatus status;
            if (rng.NextDouble() < attendProbability)
                status = rng.NextDouble() < 0.88 ? AttendanceStatus.Present : AttendanceStatus.Late;
            else
                status = rng.NextDouble() < 0.80 ? AttendanceStatus.Absent : AttendanceStatus.Excused;

            switch (status)
            {
                case AttendanceStatus.Present: present++; break;
                case AttendanceStatus.Late: late++; break;
                case AttendanceStatus.Excused: excused++; break;
                default: absent++; break;
            }
            records.Add(new AttendanceRecord { AttendanceSession = session, Enrollment = enrollment, Status = status });
        }

        var counts = new AttendanceCounts(present, late, excused, absent);
        enrollment.AttendanceScore = ScoreCalculator.AttendanceScore(counts);

        var a = sim.Ability;
        var d = sim.Diligence;
        enrollment.AssignmentScore = Score(6.8 + 1.1 * a + 0.9 * d - difficulty + 0.9 * Normal(rng));
        enrollment.MidtermScore = Score(6.4 + 1.5 * a + 0.4 * d - difficulty + 1.2 * Normal(rng));

        if (past)
        {
            // Điểm cuối kỳ còn bị ảnh hưởng bởi số buổi vắng
            enrollment.FinalScore = Score(6.2 + 1.7 * a + 0.5 * d - difficulty - 0.12 * absent + 1.3 * Normal(rng));
            enrollment.TotalScore = ScoreCalculator.TotalScore(enrollment.AttendanceScore, enrollment.AssignmentScore,
                enrollment.MidtermScore, enrollment.FinalScore, weights);
            enrollment.IsPassed = ScoreCalculator.IsPassed(enrollment.TotalScore, counts.Absent, section.TotalSessions);
            sim.History.Add((enrollment.TotalScore!.Value, enrollment.IsPassed == true, credits));
        }
        else
        {
            // Giữa kỳ: chưa có điểm cuối kỳ; một số sinh viên chưa được nhập đủ điểm (dữ liệu thiếu thật)
            if (rng.NextDouble() < 0.08)
                enrollment.AssignmentScore = null;
            if (rng.NextDouble() < 0.04)
                enrollment.MidtermScore = null;
        }
    }

    /// <summary>Ngày thi không rơi vào chủ nhật (dời sang thứ hai).</summary>
    private static DateOnly ExamDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? date.AddDays(1) : date;

    private static string RandomName(Random rng)
    {
        var male = rng.NextDouble() < 0.55;
        var middle = male ? MaleMiddle : FemaleMiddle;
        var given = male ? MaleNames : FemaleNames;
        return $"{Surnames[rng.Next(Surnames.Length)]} {middle[rng.Next(middle.Length)]} {given[rng.Next(given.Length)]}";
    }

    private static double Normal(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

    private static decimal Score(double value) => Math.Round((decimal)Math.Clamp(value, 0, 10), 1);
}
