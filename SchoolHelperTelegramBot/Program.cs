using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot
{
    class Program
    {
        private static TelegramBotClient? _client;
        private static readonly string _token = "5151427908:AAFbHUIvyt1NQrzpS7mTe3GQIG7TuZHLUY0";
        private static string _form = string.Empty;
        private static string _week = string.Empty;
        private static string _day = string.Empty;

        static void Main(string[] args)
        {
            _client = new TelegramBotClient(_token);
            _client.StartReceiving();
            _client.OnMessage += OnMessageHandler;
            Console.ReadKey();
        }

        private static async void OnMessageHandler(object? sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message.Text != null && message.Text[0] == '/')
            {
                switch (message.Text)
                {
                    case "/tabletime":
                        _client.OnMessage -= OnMessageHandler;
                        await _client.SendTextMessageAsync(message.Chat.Id, "Виберіть клас", replyMarkup: GetFormButtons());
                        _client.OnMessage += OnFormHandler;
                        break;
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

        private static async void OnFormHandler(object? sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message.Text != null)
            {
                _form = message.Text;
                await _client.SendTextMessageAsync(message.Chat.Id, "Виберіть неділю", replyMarkup: GetWeekButtons());
            }

            _client.OnMessage -= OnFormHandler;
            _client.OnMessage += OnWeekHandler;
        }

        private static async void OnWeekHandler(object? sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message.Text != null)
            {
                _week = message.Text;
                await _client.SendTextMessageAsync(message.Chat.Id, "Виберіть день", replyMarkup: GetDayButtons());
            }

            _client.OnMessage -= OnWeekHandler;
            _client.OnMessage += OnPostHandler;
        }

        private static async void OnPostHandler(object? sender, MessageEventArgs e)
        {
            var message = e.Message;

            try
            {
                if (message.Text != null)
                {
                    _day = message.Text;
                    await _client.SendTextMessageAsync(message.Chat.Id, "Ось ваш розклад");

                    using (Stream stream = File.OpenRead($@"{Environment.CurrentDirectory}\Resources\{_form} {_day} {_week}.png"))
                    {
                        _client.SendPhotoAsync(message.Chat.Id, stream);
                    }
                }
            }
            catch (Exception exc)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, exc.Message);
                Console.WriteLine(exc.Message);
            }
           
            _client.OnMessage -= OnPostHandler;
            _client.OnMessage += OnMessageHandler;
        }

        private static IReplyMarkup GetFormButtons()
        {
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ /*new KeyboardButton { Text = "5 А"}, new KeyboardButton { Text = "5 Б" },*/ new KeyboardButton { Text = "5 В" } },
                    //new List<KeyboardButton>{ new KeyboardButton { Text = "6 А"}, new KeyboardButton { Text = "6 Б" }, new KeyboardButton { Text = "6 В" } }
                }
            };
        }

        private static IReplyMarkup GetWeekButtons()
        {
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "1"}/*, new KeyboardButton { Text = "2" }, new KeyboardButton { Text = "3" }, new KeyboardButton { Text = "4" }*/ }
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
}