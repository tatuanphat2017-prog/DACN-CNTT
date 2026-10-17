using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Các DbSet (Users, Courses, ...) sẽ được thêm sau khi chốt ERD
}
