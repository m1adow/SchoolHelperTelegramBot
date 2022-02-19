using Telegram.Bot;
using Telegram.Bot.Args;
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
    EnterWeekAdmin = 5
}

class Program
{
    private static TelegramBotClient? _client;
    private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";
    private static byte _week;

    private static List<User> _users = new();

    private static Dictionary<string, string> _days = new Dictionary<string, string>()
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
        public string Form { get; set; }
        public string Week { get; set; }
        public string Day { get; set; }
        public int CountOfSignIn { get; set; }
        public UserState State { get; set; }
    }

    static void Main(string[] args)
    {
        _week = 1;
        _client = new TelegramBotClient(_token);
        _client.StartReceiving();
        _client.OnMessage += OnMessageHandler;
        Console.ReadKey();
    }

    private static void PrintLog(Telegram.Bot.Types.Message message, User? user)
    {
        Console.WriteLine($"{DateTime.Now.TimeOfDay} Message: {message.Text} From: {message.From.Username}({message.From.Id}) State: {user.State}");
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
                currentUser.Week = message.Text;
                currentUser.State = UserState.EnterDay;

                await _client.SendTextMessageAsync(currentUser.ChatId, "Виберіть день", replyMarkup: GetDayButtons());
                return;
            }

            if (currentUser.State == UserState.EnterDay)
            {
                _days.TryGetValue(message.Text, out string day);
                currentUser.Day = day;
                currentUser.State = UserState.Basic;

                using (Stream stream = File.OpenRead($@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{currentUser.Day}_{currentUser.Week}.png"))
                {
                    InputOnlineFile inputOnlineFile = new(stream);
                    await _client.SendPhotoAsync(currentUser.ChatId, inputOnlineFile);
                }

                await _client.SendTextMessageAsync(currentUser.ChatId, "Тримайте", replyMarkup: new ReplyKeyboardRemove());

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

                using (Stream stream = File.OpenRead($@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}\{DateTime.Now.DayOfWeek}_{_week}.png"))
                {
                    InputOnlineFile inputOnlineFile = new(stream);
                    await _client.SendPhotoAsync(currentUser.ChatId, inputOnlineFile);
                }

                await _client.SendTextMessageAsync(currentUser.ChatId, "Тримайте", replyMarkup: new ReplyKeyboardRemove());

                return;
            }

            if (currentUser.State == UserState.Admin)
            {
                if (currentUser.CountOfSignIn != 3)
                {
                    if (message.Text == "school5")
                    {
                        currentUser.State = UserState.EnterWeekAdmin;
                        await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть неділю");

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Admin {message.From.Username}({message.From.Id}) have entered.");
                        Console.ForegroundColor = ConsoleColor.Gray;
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

            if (currentUser.State == UserState.EnterWeekAdmin)
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

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Admin {message.From.Username}({message.From.Id}) have changed the week.");
                Console.ForegroundColor = ConsoleColor.Gray;

                currentUser.State = UserState.Basic;
                return;
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
                            if (currentUser.CountOfSignIn != 3)
                            {
                                currentUser.State = UserState.Admin;
                                await _client.SendTextMessageAsync(currentUser.ChatId, "Введіть пароль");
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
}