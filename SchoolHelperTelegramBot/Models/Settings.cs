using System.Data.SqlClient;
using Telegram.Bot;

namespace SchoolHelperTelegramBot.Models
{
    internal class Settings
    {
        public TelegramBotClient? Client { get; private set; }
        public SqlConnection? SqlConnection { get; private set; }

        public string? Password { get; private set; }
        public byte Week { get; private set; }

        public List<User>? Users { get; private set; }
        public List<string>? Advices { get; private set; }

        public Dictionary<string, double>? CountOfRequests { get; private set; }

        public Settings(TelegramBotClient? telegramBotClient, SqlConnection? sqlConnection, string? password)
        {
            Client = telegramBotClient;
            SqlConnection = sqlConnection;
            Password = password;
            Week = 1;
            Users = new List<User>();
            Advices = new List<string>();
            CountOfRequests = new Dictionary<string, double>();
        }

        public void ChangePassword(string? password) => Password = password;

        public void ChangeWeek(byte week) => Week = week;

        public void AddUserRequest(string? command)
        {
            if (!CountOfRequests.Any(x => x.Key == command)) CountOfRequests.Add(command, 1);
            else CountOfRequests[command]++;
        }
    }
}