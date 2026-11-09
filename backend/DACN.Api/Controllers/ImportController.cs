using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Nhập dữ liệu từ file Excel. Chỉ Admin.</summary>
[ApiController]
[Route("api/import")]
[Authorize(Roles = Roles.Admin)]
public class ImportController(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config) : ApiControllerBase
{
    private const int MaxRows = 500;
    private const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Nhập danh sách sinh viên từ file .xlsx (gửi dạng multipart/form-data, field tên "file", tối đa 5MB và 500 dòng).
    /// Dòng đầu là tiêu đề, cột bắt buộc: MSSV, Họ tên, Mã khoa, Lớp, Năm nhập học (cột Email tùy chọn).
    /// Mỗi dòng tạo một tài khoản sinh viên với username = MSSV. Mật khẩu ban đầu là cấu hình Import:DefaultPassword,
    /// nếu không cấu hình thì bằng chính MSSV (nên đổi sau). Dòng lỗi được báo trong Errors, các dòng đúng vẫn được tạo.
    /// </summary>
    [HttpPost("students")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportStudents(IFormFile file)
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return InvalidField("file", "Chỉ nhận file .xlsx");
        if (file.Length is 0 or > MaxFileBytes)
            return InvalidField("file", "File rỗng hoặc lớn hơn 5MB");

        IXLWorksheet sheet;
        XLWorkbook workbook;
        try
        {
            await using var stream = file.OpenReadStream();
            workbook = new XLWorkbook(stream);
            sheet = workbook.Worksheets.First();
        }
        catch (Exception)
        {
            return InvalidField("file", "Không đọc được file Excel (file hỏng hoặc không phải .xlsx)");
        }

        using (workbook)
        {
            var headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
                return InvalidField("file", "File không có dữ liệu");

            var columns = MapColumns(headerRow);
            var missing = new[] { "mssv", "hoten", "makhoa", "lop", "namnhaphoc" }.Where(c => !columns.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                return InvalidField("file", "Thiếu cột bắt buộc: MSSV, Họ tên, Mã khoa, Lớp, Năm nhập học");

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            if (lastRow - headerRow.RowNumber() > MaxRows)
                return InvalidField("file", $"Tối đa {MaxRows} dòng mỗi lần nhập");

            return Ok(await ProcessRowsAsync(sheet, columns, headerRow.RowNumber() + 1, lastRow));
        }
    }

    private async Task<ImportResult> ProcessRowsAsync(IXLWorksheet sheet, Dictionary<string, int> columns, int firstRow, int lastRow)
    {
        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Code.ToUpperInvariant(), d => d.Id);
        var existingCodes = (await db.Students.AsNoTracking().Select(s => s.StudentCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingUsernames = (await db.Users.AsNoTracking().Select(u => u.Username).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = (await db.Users.AsNoTracking().Where(u => u.Email != null).Select(u => u.Email!).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaultPassword = config["Import:DefaultPassword"];
        var sharedHash = string.IsNullOrWhiteSpace(defaultPassword) ? null : hasher.HashPassword(new User(), defaultPassword);

        var errors = new List<ImportError>();
        var created = 0;
        var total = 0;

        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            string Cell(string key) => columns.TryGetValue(key, out var col) ? row.Cell(col).GetString().Trim() : "";

            var code = Cell("mssv");
            var name = Cell("hoten");
            var deptCode = Cell("makhoa");
            var className = Cell("lop");
            var yearText = Cell("namnhaphoc");
            var email = Cell("email");
            if (new[] { code, name, deptCode, className, yearText, email }.All(string.IsNullOrEmpty))
                continue;   // dòng trống

            total++;
            var problem = Check(code, name, className, yearText, email, out var year)
                          ?? (departments.ContainsKey(deptCode.ToUpperInvariant()) ? null : $"Mã khoa {deptCode} không hợp lệ")
                          ?? (existingCodes.Contains(code) || existingUsernames.Contains(code) ? $"MSSV {code} đã tồn tại" : null)
                          ?? (email.Length > 0 && existingEmails.Contains(email) ? $"Email {email} đã được dùng" : null);
            if (problem is not null)
            {
                errors.Add(new ImportError(rowNumber, problem));
                continue;
            }

            var user = new User { Username = code, FullName = name, Email = email.Length > 0 ? email : null, Role = UserRole.Student };
            user.PasswordHash = sharedHash ?? hasher.HashPassword(user, code);
            user.Student = new Student
            {
                StudentCode = code, DepartmentId = departments[deptCode.ToUpperInvariant()],
                ClassName = className, EnrollmentYear = year,
            };
            db.Users.Add(user);
            existingCodes.Add(code);
            existingUsernames.Add(code);
            if (email.Length > 0)
                existingEmails.Add(email);
            created++;
        }

        await db.SaveChangesAsync();
        return new ImportResult(total, created, errors.Count, errors);
    }

    private static string? Check(string code, string name, string className, string yearText, string email, out int year)
    {
        year = 0;
        if (code.Length is 0 or > 20)
            return "MSSV trống hoặc dài quá 20 ký tự";
        if (name.Length is 0 or > 100)
            return "Họ tên trống hoặc dài quá 100 ký tự";
        if (className.Length is 0 or > 50)
            return "Lớp trống hoặc dài quá 50 ký tự";
        if (!int.TryParse(yearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) || year is < 2000 or > 2100)
            return "Năm nhập học không hợp lệ (cần số từ 2000 đến 2100)";
        if (email.Length > 100 || (email.Length > 0 && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)))
            return "Email không hợp lệ";
        return null;
    }

    // Nhận tiêu đề cột không phân biệt hoa thường và dấu tiếng Việt: "Họ tên", "ho ten", "HỌ TÊN" đều được
    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            map.TryAdd(Normalize(cell.GetString()), cell.Address.ColumnNumber);
        }
        return map;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                builder.Append(c);
        }
        return builder.ToString();
    }
}
