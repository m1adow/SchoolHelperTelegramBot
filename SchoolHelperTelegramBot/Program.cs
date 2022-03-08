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
    private static Settings? _settings;
    private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";

    private static readonly Dictionary<string, string> _days = new()
    {
        ["Понедiлок"] = "Monday",
        ["Вiвторок"] = "Tuesday",
        ["Середа"] = "Wednesday",
        ["Четвер"] = "Thursday",
        ["П'ятниця"] = "Friday"
    };

    static void Main(string[] args)
    {
        Start();
        Console.ReadLine();
    }

    private static void Start()
    {
        LaunchClient(out TelegramBotClient? client);
        ConnectToDataBase(out SqlConnection? sqlConnection);

        _settings = new(client, sqlConnection, "school5");
    }

    private static void LaunchClient(out TelegramBotClient? client)
    {
        client = new TelegramBotClient(_token);
        client.StartReceiving();
        client.OnMessage += OnMessageHandler;
    }

    private static void ConnectToDataBase(out SqlConnection? sqlConnection)
    {
        sqlConnection = new SqlConnection($@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={Environment.CurrentDirectory}\Resources\DataBase\School.mdf;Integrated Security=True");
        //sqlConnection = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\study\codes VS\SchoolHelperTelegramBot\SchoolHelperTelegramBot\School.mdf;Integrated Security=True");
        sqlConnection.Open();

        Console.ForegroundColor = ConsoleColor.Magenta;
        if (sqlConnection.State == ConnectionState.Open) Console.WriteLine("Connection was established");
        else Console.WriteLine("Connection wasn't established");
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static void PrintLog(Message message, Models.User? user) => Console.WriteLine($"{DateTime.Now.TimeOfDay} Message: {message.Text} From: {message.From.Username}({message.From.Id}) State: {user.State}");

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
                await client.SendPhotoAsync(user.ChatId, inputOnlineFile, replyMarkup: new ReplyKeyboardRemove());
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
            await client.SendTextMessageAsync(user.ChatId, "Перевірте вірність даних", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private static async Task<bool> GetTeacher(TelegramBotClient? client, SqlConnection sqlConnection, Models.User? user, string request)
    {
        try
        {
            SqlCommand sqlCommand = new(request, sqlConnection);
            SqlDataReader dataReader = sqlCommand.ExecuteReader();

            while (dataReader.Read()) await client.SendTextMessageAsync(user.ChatId, $"{dataReader["Name"]}. E-Mail: {dataReader["E-Mail"]}");

            if (!dataReader.HasRows)
            {
                await client.SendTextMessageAsync(user.ChatId, "Не існує такого вчителя, введіть справжні дані");
                dataReader.Close();
                return false;
            }

            dataReader.Close();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
            return false;
        }

        return true;
    }

    private static void ActWithTeacher(SqlConnection sqlConnection, string request)
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
            await client.SendTextMessageAsync(user.ChatId, "Введіть коректне значення", replyMarkup: Buttons.FormButtons());
            return false;
        }

        return true;
    }

    private async static Task<bool> IsFormExist(TelegramBotClient? client, Models.User? user)
    {
        if (user.ConstantForm is null)
        {
            await client.SendTextMessageAsync(user.ChatId, "Вкажіть клас в налаштуваннях - /settings", replyMarkup: new ReplyKeyboardRemove());
            return false;
        }

        return true;
    }

    private static async void ClearAdmins(List<Models.User> users, TelegramBotClient? client)
    {
        foreach (Models.User user in users)
        {
            if (user.IsAdmin)
            {
                user.IsAdmin = false;
                user.State = UserState.Basic;
                await client.SendTextMessageAsync(user.ChatId, "Пароль від адмін акаунту був змінен. Увійдіть знову", replyMarkup: new ReplyKeyboardRemove());
            }
        }
    }

    private static async void OnMessageHandler(object? sender, MessageEventArgs e)
    {
        try
        {
            Models.User? currentUser = _settings.Users.FirstOrDefault(u => u.ChatId == e.Message.Chat.Id);

            if (currentUser is null)
            {
                currentUser = new Models.User()
                {
                    ChatId = e.Message.Chat.Id,
                    CountOfSignIn = 0,
                    IsAdmin = false,
                    State = UserState.Basic
                };

                _settings.Users.Add(currentUser);
            }

            if (currentUser is null) throw new NullReferenceException();

            var message = e.Message;
            PrintLog(message, currentUser);

            if (currentUser.State == UserState.Settings)
            {
                if (!IsFormRight(_settings.Client, message, currentUser).Result) return;

                currentUser.ConstantForm = message.Text;
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Ви змінили свій клас на {currentUser.ConstantForm}", replyMarkup: new ReplyKeyboardRemove());
                currentUser.State = UserState.Basic;
                return;
            }

            if (currentUser.State == UserState.EnterTeacher)
            {
                if (message.Text != null)
                    if (!GetTeacher(_settings.Client, _settings.SqlConnection, currentUser, $@"SELECT Name, [E-Mail], Phone FROM Teacher WHERE Name LIKE N'%{message.Text}%'").Result)
                        return;

                currentUser.State = UserState.Basic;
                return;
            }

            if (currentUser.State == UserState.EnterForm)
            {
                if (!IsFormRight(_settings.Client, message, currentUser).Result) return;

                currentUser.Form = message.Text;
                currentUser.State = UserState.EnterWeek;

                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Виберіть тиждень", replyMarkup: Buttons.WeekButtons());
                return;
            }

            if (currentUser.State == UserState.EnterWeek)
            {
                if (int.Parse(message.Text) <= 0 || int.Parse(message.Text) > 4)
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Виберіть значення від 1 до 4", replyMarkup: Buttons.WeekButtons());
                    return;
                }

                currentUser.Week = message.Text;
                currentUser.State = UserState.EnterDay;

                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Виберіть день", replyMarkup: Buttons.DayButtons());
                return;
            }

            if (currentUser.State == UserState.EnterDay)
            {
                if (!_days.Any(a => a.Key == message.Text))
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Виберіть коректне значення", replyMarkup: Buttons.DayButtons());
                    return;
                }

                _days.TryGetValue(message.Text, out string? day);
                currentUser.Day = day;
                currentUser.State = UserState.Basic;

                SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{currentUser.Day}_{currentUser.Week}.png");
                return;
            }

            if (currentUser.State == UserState.EnterAdvice)
            {
                if (message.Text is null)
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть текст", replyMarkup: new ReplyKeyboardRemove());
                    return;
                }

                _settings.Advices.Add(message.Text);
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Ваше побажання буде побачено", replyMarkup: new ReplyKeyboardRemove());
                currentUser.State = UserState.Basic;
                return;
            }

            if (currentUser.State == UserState.AdminSignIn)
            {
                if (currentUser.CountOfSignIn != 3)
                {
                    if (message.Text == _settings.Password)
                    {
                        currentUser.State = UserState.Admin;
                        currentUser.IsAdmin = true;
                        await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду", replyMarkup: Buttons.AdminCommands());

                        PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log in.");
                    }
                    else
                    {
                        await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Залишилось спроб - {3 - currentUser.CountOfSignIn}.");
                        currentUser.CountOfSignIn++;
                    }
                }
                else if (currentUser.CountOfSignIn == 3)
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Вами було введено багато помилкових паролей.");
                    currentUser.State = UserState.Basic;
                }

                return;
            }

            if (currentUser.State == UserState.ChangePasswordAdmin)
            {
                if (message.Text is not null) _settings.ChangePassword(message.Text);

                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Пароль змінен на {_settings.Password}", replyMarkup: Buttons.AdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have changed the password to \"{_settings.Password}\".");
                ClearAdmins(_settings.Users, _settings.Client);
                currentUser.State = UserState.Basic;
                return;
            }

            if (currentUser.State == UserState.ChangeWeekAdmin)
            {
                byte.TryParse(message.Text, out byte digit);

                if (message.Text.Any(c => char.IsLetter(c)))
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть дійсне значення.");
                    return;
                }

                if (digit <= 0 || digit > 4)
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть значення від 1 до 4.");
                    return;
                }

                _settings.ChangeWeek(digit);
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Неділя змінена на {_settings.Week}.", replyMarkup: Buttons.AdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have changed the week.");
                currentUser.State = UserState.Admin;
                return;
            }

            if (currentUser.State == UserState.EnterTeacherNameForAddAdmin)
            {
                currentUser.TeacherName = message.Text;
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть почту вчителя", replyMarkup: new ReplyKeyboardRemove());
                currentUser.State = UserState.EnterTeacherEMailForAddAdmin;
                return;
            }

            if (currentUser.State == UserState.EnterTeacherEMailForAddAdmin)
            {
                currentUser.TeacherEmail = message.Text;
                ActWithTeacher(_settings.SqlConnection, $@"INSERT INTO Teacher (Name, [E-Mail]) VALUES (N'{currentUser.TeacherName}', N'{currentUser.TeacherEmail}')");
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Успішно добавлен учитель \"{currentUser.TeacherName}\" з поштою \"{currentUser.TeacherEmail}\"", replyMarkup: Buttons.AdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have added the teacher with name \"{currentUser.TeacherName}\" and with E-Mail \"{currentUser.TeacherEmail}\"");
                currentUser.State = UserState.Admin;
                return;
            }

            if (currentUser.State == UserState.EnterTeacherNameForDeleteAdmin)
            {
                ActWithTeacher(_settings.SqlConnection, $"DELETE FROM Teacher WHERE Name LIKE N'{message.Text}'");
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, $"Успішно видален учитель \"{message.Text}\"", replyMarkup: Buttons.AdminCommands());
                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have deleted the teacher with name \"{message.Text}\"");
                currentUser.State = UserState.Admin;
                return;
            }

            if (currentUser.State == UserState.EnterAdAdmin)
            {
                if (message.Text is null)
                {
                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть текст", replyMarkup: new ReplyKeyboardMarkup());
                    return;
                }

                _settings.Users.ForEach(async u => await _settings.Client.SendTextMessageAsync(u.ChatId, message.Text));
                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Успішно відправлено оголошення", replyMarkup: Buttons.AdminCommands());
                currentUser.State = UserState.Admin;
                return;
            }

            if (currentUser.State == UserState.Admin)
            {
                if (message.Text != null)
                {
                    switch (message.Text)
                    {
                        case "Зробити оголошення":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть текст з оголошенням", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.EnterAdAdmin;
                            return;
                        case "Змiнити недiлю":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть неділю", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.ChangeWeekAdmin;
                            return;
                        case "Змiнити пароль":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть новий пароль", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.ChangePasswordAdmin;
                            return;
                        case "Получити усiх вчителiв":
                            await GetTeacher(_settings.Client, _settings.SqlConnection, currentUser, "SELECT Name, [E-Mail], Phone FROM Teacher");
                            return;
                        case "Додати вчителя":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть i'мя вчителя", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.EnterTeacherNameForAddAdmin;
                            return;
                        case "Видалити вчителя":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть i'мя вчителя", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.EnterTeacherNameForDeleteAdmin;
                            return;
                        case "Перезагрузити бота":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Перезагрузка... Вас буде вилучено з адмін акаунту.", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have restarted the bot.");
                            Start();
                            return;
                        case "Очистити пам'ять":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Очищення пам'яті... Вас буде вилучено з адмін акаунту.", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have cleared the bot.");
                            _settings.Users.Clear();
                            return;
                        case "Статистика запитiв":
                            string stats = string.Empty;
                            List<string> keys = new(_settings.CountOfRequests.Keys.Count);
                            double countOfRequests = 0;

                            foreach (var item in _settings.CountOfRequests)
                            {
                                keys.Add(item.Key);
                                countOfRequests += item.Value;
                                stats += $"Команда - {item.Key}. Кількість запросів - {item.Value}\n";
                            }

                            stats += $"\nЗагальна кількість запитів - {countOfRequests}\n";

                            for (int i = 0; i < keys.Count; i++)
                            {
                                _settings.CountOfRequests.TryGetValue(keys[i], out double value);
                                stats += $"{keys[i]} - {Math.Round(value / countOfRequests * 100, 2)}%\n";
                            }

                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, stats, replyMarkup: Buttons.AdminCommands());
                            return;
                        case "Получити всi побажання":
                            if (_settings.Advices.Count == 0)
                            {
                                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Побажань немає", replyMarkup: Buttons.AdminCommands());
                                return;
                            }
                            else
                            {
                                foreach (var item in _settings.Advices) await _settings.Client.SendTextMessageAsync(currentUser.ChatId, item, replyMarkup: new ReplyKeyboardRemove());

                                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Це все", replyMarkup: Buttons.AdminCommands());
                            }

                            return;
                        case "Видалити всі побажання":
                            _settings.Advices.Clear();
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Успішно видалені всі побажання", replyMarkup: Buttons.AdminCommands());
                            return;
                        case "Вийти":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Ви вийшли з адмін акаунту", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log out from account.");
                            currentUser.IsAdmin = false;
                            currentUser.State = UserState.Basic;
                            return;
                        default:
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Не існує такої команди");
                            break;
                    }
                }
                else await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду");
            }

            if (currentUser.State == UserState.Basic)
            {
                if (message.Text != null && message.Text[0] == '/')
                {
                    switch (message.Text)
                    {
                        case "/tabletime":
                            _settings.AddUserRequest(message.Text);
                            currentUser.State = UserState.EnterForm;
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть клас", replyMarkup: Buttons.FormButtons());
                            return;
                        case "/today":
                            _settings.AddUserRequest(message.Text);

                            if (!IsFormExist(_settings.Client, currentUser).Result) return;

                            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                            {
                                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Сьогодні вихідний, отже тримайте на понеділок", replyMarkup: new ReplyKeyboardRemove());
                                SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.ConstantForm}\Monday_{_settings.Week}.png");
                                return;
                            }

                            SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.ConstantForm}\{DateTime.Now.DayOfWeek}_{_settings.Week}.png");
                            return;
                        case "/tomorrow":
                            _settings.AddUserRequest(message.Text);

                            if (!IsFormExist(_settings.Client, currentUser).Result) return;

                            if (DateTime.Now.DayOfWeek + 1 == DayOfWeek.Saturday || DateTime.Now.DayOfWeek + 1 == DayOfWeek.Sunday)
                            {
                                await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Завтра вихідний, отже тримайте на понеділок", replyMarkup: new ReplyKeyboardRemove());
                                SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.ConstantForm}\Monday_{_settings.Week}.png");
                                return;
                            }

                            SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\{currentUser.ConstantForm}\{DateTime.Now.DayOfWeek + 1}_{_settings.Week}.png");
                            return;
                        case "/bells":
                            _settings.AddUserRequest(message.Text);
                            SendPhoto(_settings.Client, currentUser, $@"{Environment.CurrentDirectory}\Resources\bells.png");
                            return;
                        case "/teacher":
                            _settings.AddUserRequest(message.Text);
                            currentUser.State = UserState.EnterTeacher;
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть прізвище", replyMarkup: new ReplyKeyboardRemove());
                            return;
                        case "/advice":
                            _settings.AddUserRequest(message.Text);
                            currentUser.State = UserState.EnterAdvice;
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть ваше побажання", replyMarkup: new ReplyKeyboardRemove());
                            return;
                        case "/settings":
                            _settings.AddUserRequest(message.Text);
                            currentUser.State = UserState.Settings;
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: Buttons.FormButtons());
                            return;
                        case "/admin":
                            _settings.AddUserRequest(message.Text);

                            if (currentUser.IsAdmin == true)
                            {
                                currentUser.State = UserState.Admin;
                                return;
                            }
                            else
                            {
                                if (currentUser.CountOfSignIn != 3)
                                {
                                    currentUser.State = UserState.AdminSignIn;
                                    await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть пароль");
                                }
                                else await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "У вас більше не має можливості ввійти у цей аккаунт.");
                            }
                            return;
                        case "/clear":
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Очищення клавіатури", replyMarkup: new ReplyKeyboardRemove());
                            return;
                        default:
                            await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Не існує такої команди");
                            break;
                    }
                }
                else await _settings.Client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду");
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