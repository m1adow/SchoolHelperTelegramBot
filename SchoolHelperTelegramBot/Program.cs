using SchoolHelperTelegramBot.Models;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot;

class Program
{
    private static TelegramBotClient? _client;
    private static SqlConnection? _sqlConnection;
    private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";
    private static byte _week;

    private static List<Models.User> _users = new();

    private static Dictionary<string, string> _days = new()
    {
        ["Понедiлок"] = "Monday",
        ["Вiвторок"] = "Tuesday",
        ["Середа"] = "Wednesday",
        ["Четвер"] = "Thursday",
        ["П'ятниця"] = "Friday"
    };

    static void Main(string[] args)
    {
        ConnectToDataBase(out _sqlConnection);
        _week = 1;
        _client = new TelegramBotClient(_token);
        _client.StartReceiving();
        _client.OnMessage += OnMessageHandler;
        Console.ReadKey();
    }

    private static void ConnectToDataBase(out SqlConnection? sqlConnection)
    {
        sqlConnection = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\study\codes VS\SchoolHelperTelegramBot\SchoolHelperTelegramBot\School.mdf;Integrated Security=True");
        sqlConnection.Open();

        Console.ForegroundColor = ConsoleColor.Magenta;
        if (sqlConnection.State == ConnectionState.Open) Console.WriteLine("Connection was established");
        else Console.WriteLine("Connection wasn't established");
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static void PrintLog(Message message, Models.User? user)
    {
        Console.WriteLine($"{DateTime.Now.TimeOfDay} Message: {message.Text} From: {message.From.Username}({message.From.Id}) State: {user.State}");
    }

    private static void PrintAdminAct(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static async void SendPhoto(TelegramBotClient? client, Models.User? user, string path)
    {
        try
        {
            using (Stream stream = System.IO.File.OpenRead(path))
            {
                InputOnlineFile inputOnlineFile = new(stream);
                await client.SendPhotoAsync(user.ChatId, inputOnlineFile);
            }

            await client.SendTextMessageAsync(user.ChatId, "Тримайте", replyMarkup: new ReplyKeyboardRemove());
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
            await client.SendTextMessageAsync(user.ChatId, "Перевірте вірність даних", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private static async void GetTeacher(TelegramBotClient? client, SqlConnection sqlConnection, Models.User? user, string request)
    {
        try
        {
            SqlCommand sqlCommand = new(request, sqlConnection);
            SqlDataReader dataReader = sqlCommand.ExecuteReader();

            while (dataReader.Read()) await client.SendTextMessageAsync(user.ChatId, $"{dataReader["Name"]}. E-Mail: {dataReader["E-Mail"]}");

            if (!dataReader.HasRows)
            {
                await _client.SendTextMessageAsync(user.ChatId, "Не існує такого вчителя, введіть справжні дані");
                dataReader.Close();
                return;
            }

            dataReader.Close();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
            return;
        }
    }

    private static void ActWithTeacher(SqlConnection sqlConnection, Models.User? user, string request)
    {
        try
        {
            SqlCommand sqlCommand = new(request, sqlConnection);
            sqlCommand.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
            return;
        }
    }

    private static bool CheckForm(Message message, string pattern)
    {
        Regex regex = new(pattern);

        if (regex.IsMatch(message.Text)) return true;
        else return false;
    }

    private async static Task<bool> IsFormRight(TelegramBotClient? client, Message message, Models.User? user)
    {
        if (!CheckForm(message, "[0-11]{2}-[А-В]{1}") && !CheckForm(message, "[5-9]{1}-[А-В]{1}"))
        {
            await client.SendTextMessageAsync(user.ChatId, "Введіть коректне значення", replyMarkup: Settings.GetFormButtons());
            return false;
        }

        return true;
    }

    private static async void OnMessageHandler(object? sender, MessageEventArgs e)
    {
        try
        {
            Models.User? currentUser = _users.FirstOrDefault(u => u.ChatId == e.Message.Chat.Id);

            if (currentUser is null)
            {
                currentUser = new Models.User()
                {
                    ChatId = e.Message.Chat.Id,
                    CountOfSignIn = 0,
                    IsAdmin = false,
                    State = Settings.UserState.Basic
                };

                _users.Add(currentUser);
            }

            if (currentUser is null) throw new NullReferenceException();

            var message = e.Message;
            PrintLog(message, currentUser);

            if (currentUser.State == Settings.UserState.EnterTeacher)
            {
                if (message.Text != null) GetTeacher(_client, _sqlConnection, currentUser, $@"SELECT Name, [E-Mail], Phone FROM Teacher WHERE Name LIKE N'%{message.Text}%'");

                currentUser.State = Settings.UserState.Basic;
                return;
            }

            if (currentUser.State == Settings.UserState.EnterForm)
            {
                if (!IsFormRight(_client, message, currentUser).Result) return;

                currentUser.Form = message.Text;
                currentUser.State = Settings.UserState.EnterWeek;

                await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть тиждень", replyMarkup: Settings.GetWeekButtons());
                return;
            }

            if (currentUser.State == Settings.UserState.EnterWeek)
            {
                if (int.Parse(message.Text) <= 0 || int.Parse(message.Text) > 4)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть значення від 1 до 4", replyMarkup: Settings.GetWeekButtons());
                    return;
                }

                currentUser.Week = message.Text;
                currentUser.State = Settings.UserState.EnterDay;

                await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть день", replyMarkup: Settings.GetDayButtons());
                return;
            }

            if (currentUser.State == Settings.UserState.EnterDay)
            {
                if (!_days.Any(a => a.Key == message.Text))
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть коректне значення", replyMarkup: Settings.GetDayButtons());
                    return;
                }

                _days.TryGetValue(message.Text, out string? day);
                currentUser.Day = day;
                currentUser.State = Settings.UserState.Basic;

                SendPhoto(_client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{currentUser.Day}_{_week}.png");
                return;
            }

            if (currentUser.State == Settings.UserState.EnterFormToday)
            {
                if (!IsFormRight(_client, message, currentUser).Result) return;

                currentUser.Form = message.Text;
                currentUser.State = Settings.UserState.Basic;

                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Сьогодні вихідний", replyMarkup: new ReplyKeyboardRemove());
                    return;
                }

                SendPhoto(_client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{DateTime.Now.DayOfWeek}_{_week}.png");
                return;
            }

            if (currentUser.State == Settings.UserState.EnterFormTommorow)
            {
                if (!IsFormRight(_client, message, currentUser).Result) return;

                currentUser.Form = message.Text;
                currentUser.State = Settings.UserState.Basic;

                if (DateTime.Now.DayOfWeek + 1 == DayOfWeek.Saturday || DateTime.Now.DayOfWeek + 1 == DayOfWeek.Sunday)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Завтра вихідний", replyMarkup: new ReplyKeyboardRemove());
                    return;
                }

                SendPhoto(_client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{DateTime.Now.DayOfWeek + 1}_{_week}.png");
                return;
            }

            if (currentUser.State == Settings.UserState.AdminSignIn)
            {
                if (currentUser.CountOfSignIn != 3)
                {
                    if (message.Text == "school5")
                    {
                        currentUser.State = Settings.UserState.Admin;
                        currentUser.IsAdmin = true;
                        await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду", replyMarkup: Settings.GetAdminCommands());

                        PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log in.");
                    }
                    else
                    {
                        await _client.SendTextMessageAsync(currentUser.ChatId, $"Залишилось спроб - {3 - currentUser.CountOfSignIn}.");
                        currentUser.CountOfSignIn++;
                    }
                }
                else if (currentUser.CountOfSignIn == 3)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Вами було введено багато помилкових паролей.");
                    currentUser.State = Settings.UserState.Basic;
                }

                return;
            }

            if (currentUser.State == Settings.UserState.ChangeWeekAdmin)
            {
                byte.TryParse(message.Text, out byte digit);

                if (message.Text.Any(c => char.IsLetter(c)))
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть дійсне значення.");
                    return;
                }

                if (digit <= 0 || digit > 4)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть значення від 1 до 4.");
                    return;
                }

                _week = digit;
                await _client.SendTextMessageAsync(currentUser.ChatId, $"Неділя змінена на {_week}.", replyMarkup: Settings.GetAdminCommands());

                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have changed the week.");
                currentUser.State = Settings.UserState.Admin;
                return;
            }

            if (currentUser.State == Settings.UserState.EnterTeacherNameForAddAdmin)
            {
                currentUser.TeacherName = message.Text;
                await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть почту вчителя", replyMarkup: new ReplyKeyboardRemove());
                currentUser.State = Settings.UserState.EnterTeacherEMailForAddAdmin;
                return;
            }

            if (currentUser.State == Settings.UserState.EnterTeacherEMailForAddAdmin)
            {
                currentUser.TeacherEmail = message.Text;
                ActWithTeacher(_sqlConnection, currentUser, $@"INSERT INTO Teacher (Name, [E-Mail]) VALUES (N'{currentUser.TeacherName}', N'{currentUser.TeacherEmail}')");
                await _client.SendTextMessageAsync(currentUser.ChatId, $"Успішно добавлен учитель \"{currentUser.TeacherName}\" з поштою \"{currentUser.TeacherEmail}\"", replyMarkup: Settings.GetAdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have added the teacher with name \"{currentUser.TeacherName}\" and with E-Mail \"{currentUser.TeacherEmail}\"");
                currentUser.State = Settings.UserState.Admin;
                return;
            }

            if (currentUser.State == Settings.UserState.EnterTeacherNameForDeleteAdmin)
            {
                ActWithTeacher(_sqlConnection, currentUser, $"DELETE FROM Teacher WHERE Name LIKE N'{message.Text}'");
                await _client.SendTextMessageAsync(currentUser.ChatId, $"Успішно видален учитель \"{message.Text}\"", replyMarkup: Settings.GetAdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have deleted the teacher with name \"{message.Text}\"");
                currentUser.State = Settings.UserState.Admin;
                return;
            }

            if (currentUser.State == Settings.UserState.Admin)
            {
                if (message.Text != null)
                {
                    switch (message.Text)
                    {
                        case "Змiнити недiлю":
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть неділю", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = Settings.UserState.ChangeWeekAdmin;
                            return;
                        case "Змiнити розклад":
                            return;
                        case "Получити усiх вчителiв":
                            GetTeacher(_client, _sqlConnection, currentUser, "SELECT Name, [E-Mail], Phone FROM Teacher");
                            return;
                        case "Додати вчителя":
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть i'мя вчителя", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = Settings.UserState.EnterTeacherNameForAddAdmin;
                            return;
                        case "Видалити вчителя":
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть i'мя вчителя", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = Settings.UserState.EnterTeacherNameForDeleteAdmin;
                            return;
                        case "Перезагрузити бота":
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have restarted the bot.");
                            _users.Clear();
                            _client = new TelegramBotClient(_token);
                            _client.OnMessage += OnMessageHandler;
                            _sqlConnection = null;
                            ConnectToDataBase(out _sqlConnection);
                            return;
                        case "Очистити пам'ять":
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have cleared the bot.");
                            _users.Clear();
                            return;
                        case "Вийти":
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Ви вийшли з адмін акаунту", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log out from account.");
                            currentUser.IsAdmin = false;
                            currentUser.State = Settings.UserState.Basic;
                            return;
                        default:
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Не існує такої команди");
                            break;
                    }
                }
                else await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду");
            }

            if (currentUser.State == Settings.UserState.Basic)
            {
                if (message.Text != null && message.Text[0] == '/')
                {
                    switch (message.Text)
                    {
                        case "/tabletime":
                            currentUser.State = Settings.UserState.EnterForm;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: Settings.GetFormButtons());
                            return;
                        case "/today":
                            currentUser.State = Settings.UserState.EnterFormToday;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: Settings.GetFormButtons());
                            return;
                        case "/tomorrow":
                            currentUser.State = Settings.UserState.EnterFormTommorow;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: Settings.GetFormButtons());
                            return;
                        case "/bells":
                            SendPhoto(_client, currentUser, $@"{Environment.CurrentDirectory}\Resources\bells.png");
                            return;
                        case "/teacher":
                            currentUser.State = Settings.UserState.EnterTeacher;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть прізвище");
                            return;
                        case "/admin":
                            if (currentUser.IsAdmin == true)
                            {
                                currentUser.State = Settings.UserState.Admin;
                                return;
                            }
                            else
                            {
                                if (currentUser.CountOfSignIn != 3)
                                {
                                    currentUser.State = Settings.UserState.AdminSignIn;
                                    await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть пароль");
                                }
                                else
                                {
                                    await _client.SendTextMessageAsync(currentUser.ChatId, "У вас більше не має можливості ввійти у цей аккаунт");
                                }
                            }
                            return;
                        case "/clear":
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Очищення клавіатури", replyMarkup: new ReplyKeyboardRemove());
                            return;
                        default:
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Не існує такої команди");
                            break;
                    }
                }
                else await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}