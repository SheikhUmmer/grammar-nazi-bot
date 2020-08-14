﻿using GrammarNazi.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace GrammarNazi.App.HostedServices
{
    public class BotHostedService : BackgroundService
    {
        private readonly ILogger<BotHostedService> _logger;
        private readonly IGrammarService _grammarService;
        private readonly TelegramBotClient _client;

        public BotHostedService(ILogger<BotHostedService> logger, IGrammarService grammarService)
        {
            _logger = logger;
            _grammarService = grammarService;
            var apiKey = Environment.GetEnvironmentVariable("TELEGRAM_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Empty TELEGRAM_API_KEY");

            _client = new TelegramBotClient(apiKey);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Bot Hosted Service started");

            _client.StartReceiving(cancellationToken: stoppingToken);
            _client.OnMessage += BotOnMessage;

            // Keep hosted service alive while receiving messages
            await Task.Delay(int.MaxValue, stoppingToken);
        }

        private void BotOnMessage(object sender, MessageEventArgs messageEvent)
        {
            _logger.LogInformation($"Message reveived: {messageEvent.Message.Text}");

            if (messageEvent.Message.Type == MessageType.Text)
            {
                var result = _grammarService.GetCorrections(messageEvent.Message.Text).GetAwaiter().GetResult();

                if (result.HasErrors)
                {
                    //TODO: reply with correct word

                    string message = "The following words doesn't exist: ";

                    foreach (var error in result.ResultErrors)
                    {
                        message += $"{Environment.NewLine}    - {error.WrongWord}";
                    }

                    // Fire and forget for now (It returns a Task, i.e it's awaitable)
                    _client.SendTextMessageAsync(messageEvent.Message.Chat.Id, message, replyToMessageId: messageEvent.Message.MessageId);
                }
            }
        }
    }
}