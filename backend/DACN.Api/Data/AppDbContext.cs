using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<RiskPrediction> RiskPredictions => Set<RiskPrediction>();
    public DbSet<Warning> Warnings => Set<Warning>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Quy tắc xóa: xem docs/ERD.md, mục "Quy tắc xóa"

        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();   // SQL Server tự lọc các dòng Email null
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Student>(e =>
        {
            e.Property(x => x.StudentCode).HasMaxLength(20);
            e.Property(x => x.ClassName).HasMaxLength(50);
            e.HasIndex(x => x.StudentCode).IsUnique();
            e.HasOne(x => x.User).WithOne(u => u.Student)
                .HasForeignKey<Student>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Department).WithMany(d => d.Students)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Lecturer>(e =>
        {
            e.Property(x => x.LecturerCode).HasMaxLength(20);
            e.HasIndex(x => x.LecturerCode).IsUnique();
            e.HasOne(x => x.User).WithOne(u => u.Lecturer)
                .HasForeignKey<Lecturer>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Department).WithMany(d => d.Lecturers)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Semester>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50);
            e.HasIndex(x => x.IsCurrent).IsUnique().HasFilter("[IsCurrent] = 1");
            e.ToTable(t => t.HasCheckConstraint("CK_Semesters_Dates", "[EndDate] > [StartDate]"));
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Department).WithMany(d => d.Courses)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Courses_Credits", "[Credits] > 0"));
        });

        modelBuilder.Entity<CourseSection>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.AttendanceWeight).HasPrecision(3, 2);
            e.Property(x => x.AssignmentWeight).HasPrecision(3, 2);
            e.Property(x => x.MidtermWeight).HasPrecision(3, 2);
            e.Property(x => x.FinalWeight).HasPrecision(3, 2);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Course).WithMany(c => c.CourseSections)
                .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Semester).WithMany(s => s.CourseSections)
                .HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lecturer).WithMany(l => l.CourseSections)
                .HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_CourseSections_TotalSessions", "[TotalSessions] > 0");
                t.HasCheckConstraint("CK_CourseSections_Weights",
                    "[AttendanceWeight] + [AssignmentWeight] + [MidtermWeight] + [FinalWeight] = 1");
            });
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.Property(x => x.PriorGpa).HasPrecision(4, 2);
            e.Property(x => x.AttendanceScore).HasPrecision(4, 2);
            e.Property(x => x.AssignmentScore).HasPrecision(4, 2);
            e.Property(x => x.MidtermScore).HasPrecision(4, 2);
            e.Property(x => x.FinalScore).HasPrecision(4, 2);
            e.Property(x => x.TotalScore).HasPrecision(4, 2);
            e.HasIndex(x => new { x.CourseSectionId, x.StudentId }).IsUnique();
            e.HasOne(x => x.CourseSection).WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.CourseSectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Enrollments_PriorGpa", "[PriorGpa] BETWEEN 0 AND 4");
                foreach (var column in new[] { "AttendanceScore", "AssignmentScore", "MidtermScore", "FinalScore", "TotalScore" })
                    t.HasCheckConstraint($"CK_Enrollments_{column}", $"[{column}] BETWEEN 0 AND 10");
            });
        });

        modelBuilder.Entity<AttendanceSession>(e =>
        {
            e.HasIndex(x => new { x.CourseSectionId, x.SessionNo }).IsUnique();
            e.HasOne(x => x.CourseSection).WithMany(s => s.AttendanceSessions)
                .HasForeignKey(x => x.CourseSectionId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_AttendanceSessions_SessionNo", "[SessionNo] > 0"));
        });

        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.AttendanceSessionId, x.EnrollmentId }).IsUnique();
            e.HasOne(x => x.AttendanceSession).WithMany(s => s.AttendanceRecords)
                .HasForeignKey(x => x.AttendanceSessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Enrollment).WithMany(en => en.AttendanceRecords)
                .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelVersion>(e =>
        {
            e.Property(x => x.Version).HasMaxLength(50);
            e.Property(x => x.Algorithm).HasMaxLength(50);
            e.Property(x => x.FilePath).HasMaxLength(255);
            e.HasIndex(x => x.IsActive).IsUnique().HasFilter("[IsActive] = 1");
        });

        modelBuilder.Entity<RiskPrediction>(e =>
        {
            e.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.EnrollmentId, x.PredictedAt });
            e.HasOne(x => x.Enrollment).WithMany(en => en.RiskPredictions)
                .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ModelVersion).WithMany(m => m.RiskPredictions)
                .HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_RiskPredictions_Probability", "[Probability] BETWEEN 0 AND 1"));
        });

        modelBuilder.Entity<Warning>(e =>
        {
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasOne(x => x.Enrollment).WithMany(en => en.Warnings)
                .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
            // Restrict thay vì SetNull: Users có 2 đường tới Warnings (qua Lecturers và qua Students → Enrollments)
            e.HasOne(x => x.Lecturer).WithMany(l => l.Warnings)
                .HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
