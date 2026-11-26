namespace BrickShare.Auth;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string EmployeeOrAdmin = "EmployeeOrAdmin";
    public const string CustomerOnly = "CustomerOnly";
}