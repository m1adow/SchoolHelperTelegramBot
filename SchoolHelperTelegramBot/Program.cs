using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot;

public enum UserState
{
    Basic = 0,
    EnterForm = 1,
    EnterWeek = 2,
    EnterDay = 3
}

class Program
{
    private static TelegramBotClient? _client;
    private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";


    private static List<User> _users = new();

    public class User
    {
        public long ChatId { get; set; }
        public string Form { get; set; }
        public string Week { get; set; }
        public string Day { get; set; }
        public UserState State { get; set; }
    }

    static void Main(string[] args)
    {
        _client = new TelegramBotClient(_token);
        _client.StartReceiving();
        _client.OnMessage += OnMessageHandler;
        Console.ReadKey();
    }

    private static void PrintLog(Message message, User? user)
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
                currentUser.Day = message.Text;
                currentUser.State = UserState.Basic;

                using (Stream stream = System.IO.File.Open($@"{Environment.CurrentDirectory}\Resources\{currentUser.Form}_{currentUser.Day}_{currentUser.Week}.png", FileMode.Open))
                {
                    _client.SendPhotoAsync(message.Chat.Id, stream);
                }
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
                            await _client.SendTextMessageAsync(message.Chat.Id, "Виберіть клас", replyMarkup: GetFormButtons());
                            return;
                        case "/today":
                            break;
                        case "/admin":
                            break;
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
            Console.WriteLine(ex.Message);
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
                    new List<KeyboardButton>{ new KeyboardButton { Text = "10-А"}, new KeyboardButton { Text = "10-Б" } },
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
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Понеділок" }, new KeyboardButton { Text = "Вівторок" }, new KeyboardButton { Text = "Середа" }, new KeyboardButton { Text = "Четвер" }, new KeyboardButton { Text = "П'ятниця" } }
                }
        };
    }
}