using DACN.Api.Data;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

public enum AccessStatus
{
    Ok,
    NotFound,
    Forbidden,
}

public record SectionAccess(CourseSection? Section, AccessStatus Status)
{
    public bool Ok => Status == AccessStatus.Ok;
}

/// <summary>Kiểm tra người gọi có được truy cập một lớp học phần không.</summary>
public class SectionAccessService(AppDbContext db, ICurrentUser user)
{
    /// <summary>
    /// Admin: mọi lớp. Lecturer: lớp mình dạy. Student: chỉ khi allowStudent và đã đăng ký lớp đó.
    /// Trả NotFound nếu lớp không tồn tại, Forbidden nếu có lớp nhưng không đủ quyền.
    /// </summary>
    public async Task<SectionAccess> GetAsync(int sectionId, bool allowStudent = false)
    {
        var section = await db.CourseSections.FirstOrDefaultAsync(s => s.Id == sectionId);
        if (section is null)
        {
            return new SectionAccess(null, AccessStatus.NotFound);
        }

        var allowed = user.Role switch
        {
            UserRole.Admin => true,
            UserRole.Lecturer => section.LecturerId == await user.GetLecturerIdAsync(),
            UserRole.Student => allowStudent && await IsEnrolledAsync(sectionId),
            _ => false,
        };
        return new SectionAccess(allowed ? section : null, allowed ? AccessStatus.Ok : AccessStatus.Forbidden);
    }

    private async Task<bool> IsEnrolledAsync(int sectionId)
    {
        var studentId = await user.GetStudentIdAsync();
        return studentId is not null && await db.Enrollments.AnyAsync(e => e.CourseSectionId == sectionId && e.StudentId == studentId);
    }
}
