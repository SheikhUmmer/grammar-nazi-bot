﻿using GrammarNazi.Core.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrammarNazi.Core.BotCommands.Telegram
{
    public abstract class BaseTelegramCommand
    {
        protected readonly ITelegramBotClient Client;

        public BaseTelegramCommand(ITelegramBotClient telegramBotClient)
        {
            Client = telegramBotClient;
        }

        protected async Task ShowOptions<T>(Message message, string messageTitle) where T : Enum
        {
            var enumType = typeof(T);

            var options = Enum.GetValues(enumType)
                            .Cast<T>()
                            .Select(v => new[] { InlineKeyboardButton.WithCallbackData($"{Convert.ToInt32(v)} - {v.GetDescription()}", $"{enumType.Name}.{v}") });

            var inlineOptions = new InlineKeyboardMarkup(options);

            await Client.SendTextMessageAsync(message.Chat.Id, messageTitle, replyMarkup: inlineOptions);
        }

        protected async Task<bool> IsUserAdmin(Message message, User user = null)
        {
            if (message.Chat.Type == ChatType.Private)
                return true;

            var chatAdministrators = await Client.GetChatAdministratorsAsync(message.Chat.Id);
            var currentUserId = user?.Id ?? message.From.Id;

            return chatAdministrators.Any(v => v.User.Id == currentUserId);
        }

        protected async Task<bool> IsBotAdmin(Message message)
        {
            if (message.Chat.Type == ChatType.Private)
                return true;

            var bot = await Client.GetMeAsync();
            var chatAdministrators = await Client.GetChatAdministratorsAsync(message.Chat.Id);

            return chatAdministrators.Any(v => v.User.Id == bot.Id);
        }

        protected async Task NotifyIfBotIsNotAdmin(Message message)
        {
            if (!await IsBotAdmin(message))
            {
                await Client.SendTextMessageAsync(message.Chat.Id, "NOTE: The bot needs admin rights in order to read messages from this chat.");
            }
        }

        protected async Task SendTypingNotification(Message message)
        {
            await Client.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
        }

        protected static string GetAvailableOptions<T>(T selectedOption) where T : Enum
        {
            var options = Enum.GetValues(typeof(T)).Cast<T>();

            var messageBuilder = new StringBuilder();

            foreach (var item in options)
            {
                var selected = item.Equals(selectedOption) ? "✅" : "";
                messageBuilder.AppendLine($"{Convert.ToInt32(item)} - {item.GetDescription()} {selected}");
            }

            return messageBuilder.ToString();
        }
    }
}
