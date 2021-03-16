﻿using GrammarNazi.Domain.BotCommands;
using GrammarNazi.Domain.Constants;
using GrammarNazi.Domain.Services;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GrammarNazi.Core.BotCommands.Telegram
{
    public class ShowDetailsCommand : BaseTelegramCommand, ITelegramBotCommand
    {
        private readonly IChatConfigurationService _chatConfigurationService;
        private readonly ITelegramBotClient _client;

        public string Command => TelegramBotCommands.ShowDetails;

        public ShowDetailsCommand(IChatConfigurationService chatConfigurationService,
            ITelegramBotClient telegramBotClient)
            : base(telegramBotClient)
        {
            _chatConfigurationService = chatConfigurationService;
            _client = telegramBotClient;
        }

        public async Task Handle(Message message)
        {
            if (!await IsUserAdmin(message))
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Only admins can use this command.", replyToMessageId: message.MessageId);
                return;
            }

            var chatConfig = await _chatConfigurationService.GetConfigurationByChatId(message.Chat.Id);

            chatConfig.HideCorrectionDetails = false;

            await _chatConfigurationService.Update(chatConfig);

            await _client.SendTextMessageAsync(message.Chat.Id, "Show correction details ✅");

            await NotifyIfBotIsNotAdmin(message);
        }
    }
}
