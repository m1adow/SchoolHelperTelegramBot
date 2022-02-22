using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot;

public enum UserState
{
    Admin = -1,
    Basic = 0,
    EnterForm = 1,
    EnterWeek = 2,
    EnterDay = 3,
    EnterFormToday = 4,
    EnterFormTommorow = 5,
    AdminSignIn = 6,
    ChangeWeekAdmin = 7,
    ChangeTableTimeAdmin = 8
}

class Program
{
    private static TelegramBotClient? _client;
    private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";
    private static byte _week;

    private static List<User> _users = new();

    private static Dictionary<string, string> _days = new()
    {
        ["Понедiлок"] = "Monday",
        ["Вiвторок"] = "Tuesday",
        ["Середа"] = "Wednesday",
        ["Четвер"] = "Thursday",
        ["П'ятниця"] = "Friday"
    };

    public class User
    {
        public long ChatId { get; set; }
        public string? Form { get; set; }
        public string? Week { get; set; }
        public string? Day { get; set; }
        public int CountOfSignIn { get; set; }
        public UserState State { get; set; }
        public bool IsAdmin { get; set; }
    }

    static void Main(string[] args)
    {
        _week = 1;
        _client = new TelegramBotClient(_token);
        _client.StartReceiving();
        _client.OnMessage += OnMessageHandler;
        Console.ReadKey();
    }

    private static void PrintLog(Message message, User? user)
    {
        Console.WriteLine($"{DateTime.Now.TimeOfDay} Message: {message.Text} From: {message.From.Username}({message.From.Id}) State: {user.State}");
    }

    private static void PrintAdminAct(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static async void SendPhoto(TelegramBotClient? client, User? user)
    {
        using (Stream stream = System.IO.File.OpenRead($@"{Environment.CurrentDirectory}\Resources\{user.Form}\{DateTime.Now.DayOfWeek}_{_week}.png"))
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
            User? currentUser = _users.FirstOrDefault(u => u.ChatId == e.Message.Chat.Id);

            if (currentUser is null)
            {
                currentUser = new User()
                {
                    ChatId = e.Message.Chat.Id,
                    CountOfSignIn = 0,
                    IsAdmin = false,
                    State = UserState.Basic
                };

                _users.Add(currentUser);
            }

            if (currentUser is null) throw new NullReferenceException();

            var message = e.Message;
            PrintLog(message, currentUser);

            if (currentUser.State == UserState.EnterForm)
            {
                currentUser.Form = message.Text;
                currentUser.State = UserState.EnterWeek;

                await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть тиждень", replyMarkup: GetWeekButtons());
                return;
            }

            if (currentUser.State == UserState.EnterWeek)
            {
                if (int.Parse(message.Text) <= 0 || int.Parse(message.Text) > 4)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть значення від 1 до 4", replyMarkup: GetWeekButtons());
                    return;
                }

                currentUser.Week = message.Text;
                currentUser.State = UserState.EnterDay;

                await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть день", replyMarkup: GetDayButtons());
                return;
            }

            if (currentUser.State == UserState.EnterDay)
            {
                if (!_days.Any(a => a.Key == message.Text))
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть коректне значення", replyMarkup: GetDayButtons());
                    return;
                }

                _days.TryGetValue(message.Text, out string? day);
                currentUser.Day = day;
                currentUser.State = UserState.Basic;

                SendPhoto(_client, currentUser);
                return;
            }

            if (currentUser.State == UserState.EnterFormToday)
            {
                currentUser.Form = message.Text;
                currentUser.State = UserState.Basic;

                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    await _client.SendTextMessageAsync(currentUser.ChatId, "Сьогодні вихідний", replyMarkup: new ReplyKeyboardRemove());
                    return;
                }

                SendPhoto(_client, currentUser);
                return;
            }

            if (currentUser.State == UserState.AdminSignIn)
            {
                if (currentUser.CountOfSignIn != 3)
                {
                    if (message.Text == "school5")
                    {
                        currentUser.State = UserState.Admin;
                        currentUser.IsAdmin = true;
                        await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть команду", replyMarkup: GetAdminCommands());

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
                    currentUser.State = UserState.Basic;
                }

                return;
            }

            if (currentUser.State == UserState.ChangeWeekAdmin)
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
                await _client.SendTextMessageAsync(currentUser.ChatId, $"Неділя змінена на {_week}.");

                PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have changed the week.");
                currentUser.State = UserState.Admin;
                return;
            }

            if (currentUser.State == UserState.Admin)
            {
                if (message.Text != null)
                {
                    switch (message.Text)
                    {
                        case "Змiнити недiлю":      
                            await _client.SendTextMessageAsync(message.Chat.Id, "Введіть неділю", replyMarkup: new ReplyKeyboardRemove());
                            currentUser.State = UserState.ChangeWeekAdmin;
                            return;
                        case "Змiнити розклад":
                            return;
                        case "Перезагрузити бота":                           
                            return;
                        case "Вийти":
                            await _client.SendTextMessageAsync(message.Chat.Id, "Ви вийшли з адмін акаунту", replyMarkup: new ReplyKeyboardRemove());
                            PrintAdminAct($"Admin {message.From.Username}({message.From.Id}) have log out from account.");
                            currentUser.State = UserState.Basic;
                            return;
                        default:
                            await _client.SendTextMessageAsync(message.Chat.Id, "Не існує такої команди");
                            break;
                    }
                }
                else await _client.SendTextMessageAsync(message.Chat.Id, "Введіть команду");
            }

            if (currentUser.State == UserState.Basic)
            {
                if (message.Text != null && message.Text[0] == '/')
                {
                    switch (message.Text)
                    {
                        case "/tabletime":
                            currentUser.State = UserState.EnterForm;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: GetFormButtons());
                            return;
                        case "/today":
                            currentUser.State = UserState.EnterFormToday;
                            await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть клас", replyMarkup: GetFormButtons());
                            return;
                        case "/admin":
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
                                    await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть пароль");
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

    private static IReplyMarkup GetFormButtons()
    {
        return new ReplyKeyboardMarkup
        {
            Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "5-А"}, new KeyboardButton { Text = "5-Б" }, new KeyboardButton { Text = "5-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "6-А"}, new KeyboardButton { Text = "6-Б" }, new KeyboardButton { Text = "6-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "7-А"}, new KeyboardButton { Text = "7-Б" }, new KeyboardButton { Text = "7-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "8-А"}, new KeyboardButton { Text = "8-Б" }, new KeyboardButton { Text = "8-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "9-А"}, new KeyboardButton { Text = "9-Б" }, new KeyboardButton { Text = "9-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "10-А"}, new KeyboardButton { Text = "10-Б" }, new KeyboardButton { Text = "10-В" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "11-А"}, new KeyboardButton { Text = "11-Б" } }
                }
        };
    }

    private static IReplyMarkup GetWeekButtons()
    {
        return new ReplyKeyboardMarkup
        {
            Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "1"}, new KeyboardButton { Text = "2" }, new KeyboardButton { Text = "3" }, new KeyboardButton { Text = "4" } }
                }
        };
    }

    private static IReplyMarkup GetDayButtons()
    {
        return new ReplyKeyboardMarkup
        {
            Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Понедiлок" }, new KeyboardButton { Text = "Вiвторок" }, new KeyboardButton { Text = "Середа" }, new KeyboardButton { Text = "Четвер" }, new KeyboardButton { Text = "П'ятниця" } }
                }
        };
    }

    private static IReplyMarkup GetAdminCommands()
    {
        return new ReplyKeyboardMarkup
        {
            Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Змiнити недiлю" }, new KeyboardButton { Text = "Змiнити розклад" }, new KeyboardButton { Text = "Перезагрузити бота" }, new KeyboardButton { Text = "Вийти" } }
                }
        };
    }
}