namespace SchoolHelperTelegramBot.Models
{
    internal class User
    {
        public long ChatId { get; set; }
        public string? ConstantForm { get; set; }
        public string? Form { get; set; }
        public string? Week { get; set; }
        public string? Day { get; set; }
        public int CountOfSignIn { get; set; }
        public UserState State { get; set; }
        public bool IsAdmin { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }
    }

    public enum UserState
    {
        Admin = -1,
        Basic = 0,
        Settings = 1,
        EnterForm = 10,
        EnterWeek = 11,
        EnterDay = 12,
        EnterTeacher = 13,
        EnterAdvice = 14,
        AdminSignIn = 20,
        ChangeWeekAdmin = 21,
        ChangePasswordAdmin = 22,
        EnterTeacherNameForAddAdmin = 23,
        EnterTeacherEMailForAddAdmin = 24,
        EnterTeacherNameForDeleteAdmin = 25,
        EnterAdAdmin = 26
    }
}