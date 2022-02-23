using SchoolHelperTelegramBot.Models;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot;

class Program
{
    private static TelegramBotClient? _client;
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
        _week = 1;
        _client = new TelegramBotClient(_token);
        _client.StartReceiving();
        _client.OnMessage += OnMessageHandler;
        Console.ReadKey();
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
        using (Stream stream = System.IO.File.OpenRead(path))
        {
            InputOnlineFile inputOnlineFile = new(stream);
            await client.SendPhotoAsync(user.ChatId, inputOnlineFile);
        }

        await client.SendTextMessageAsync(user.ChatId, "Тримайте", replyMarkup: new ReplyKeyboardRemove());
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
                    State = Models.Settings.UserState.Basic
                };

                _users.Add(currentUser);
            }

            if (currentUser is null) throw new NullReferenceException();

            var message = e.Message;
            PrintLog(message, currentUser);

            if (currentUser.State == Models.Settings.UserState.EnterForm)
            {
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

                        PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have entered.");
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

            if (currentUser.State == Settings.UserState.Admin)
            {
                if (message.Text != null)
                {
                    switch (message.Text)
                    {
                        case "Змiнити недiлю":
                            await _client.SendTextMessageAsync(message.Chat.Id, "Введіть неділю", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = Settings.UserState.ChangeWeekAdmin;
                            return;
                        case "Змiнити розклад":
                            return;
                        case "Перезагрузити бота":
                            _users.Clear();
                            _client = new TelegramBotClient(_token);
                            _client.OnMessage += OnMessageHandler;
                            return;
                        case "Очистити пам'ять":
                            _users.Clear();
                            return;
                        case "Вийти":
                            await _client.SendTextMessageAsync(message.Chat.Id, "Ви вийшли з адмін акаунту", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log out from account.");
                            currentUser.State = Settings.UserState.Basic;
                            return;
                        default:
                            await _client.SendTextMessageAsync(message.Chat.Id, "Не існує такої команди");
                            break;
                    }
                }
                else await _client.SendTextMessageAsync(message.Chat.Id, "Введіть команду");
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
                        default:
                            await _client.SendTextMessageAsync(message.Chat.Id, "Не існує такої команди");
                            break;
                    }
                }
                else await _client.SendTextMessageAsync(message.Chat.Id, "Введіть команду");
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