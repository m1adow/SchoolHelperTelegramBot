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
        public Settings.UserState State { get; set; }
        public bool IsAdmin { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }
    }
}