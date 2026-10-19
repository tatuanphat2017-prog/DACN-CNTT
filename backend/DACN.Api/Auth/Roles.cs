namespace DACN.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";
    public const string AdminOrLecturer = Admin + "," + Lecturer;
}
