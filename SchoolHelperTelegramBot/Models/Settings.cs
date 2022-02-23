using Telegram.Bot.Types.ReplyMarkups;

namespace SchoolHelperTelegramBot.Models
{
    internal class Settings
    {
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

        public static IReplyMarkup GetFormButtons()
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

        public static IReplyMarkup GetWeekButtons()
        {
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "1"}, new KeyboardButton { Text = "2" }, new KeyboardButton { Text = "3" }, new KeyboardButton { Text = "4" } }
                }
            };
        }

        public static IReplyMarkup GetDayButtons()
        {
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Понедiлок" }, new KeyboardButton { Text = "Вiвторок" }, new KeyboardButton { Text = "Середа" }, new KeyboardButton { Text = "Четвер" }, new KeyboardButton { Text = "П'ятниця" } }
                }
            };
        }

        public static IReplyMarkup GetAdminCommands()
        {
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Змiнити недiлю" }, new KeyboardButton { Text = "Змiнити розклад" }, new KeyboardButton { Text = "Перезагрузити бота" }, new KeyboardButton { Text = "Очистити пам'ять" }, new KeyboardButton { Text = "Вийти" } }
                }
            };
        }
    }
}