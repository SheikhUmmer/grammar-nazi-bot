﻿using GrammarNazi.Core.Extensions;
using GrammarNazi.Core.Utilities;
using GrammarNazi.Domain.Constants;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrammarNazi.App.HostedServices
{
    public class TelegramBotHostedService : BackgroundService
    {
        private readonly ILogger<TelegramBotHostedService> _logger;
        private readonly IEnumerable<IGrammarService> _grammarServices;
        private readonly IChatConfigurationService _chatConfigurationService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ITelegramBotClient _client;

        public TelegramBotHostedService(ILogger<TelegramBotHostedService> logger,
            ITelegramBotClient telegramBotClient,
            IEnumerable<IGrammarService> grammarServices,
            IChatConfigurationService chatConfigurationService,
            IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _grammarServices = grammarServices;
            _chatConfigurationService = chatConfigurationService;
            _webHostEnvironment = webHostEnvironment;
            _client = telegramBotClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Bot Hosted Service started");

            _client.StartReceiving(cancellationToken: stoppingToken);
            _client.OnMessage += async (obj, eventArgs) => await OnMessageReceived(obj, eventArgs);

            // Keep hosted service alive while receiving messages
            await Task.Delay(int.MaxValue, stoppingToken);
        }

        private async Task OnMessageReceived(object sender, MessageEventArgs messageEvent)
        {
            var message = messageEvent.Message;

            _logger.LogInformation($"Message received from chat id: {message.Chat.Id}");

            if (message.Type != MessageType.Text) // We only analyze Text messages
                return;

            if (_webHostEnvironment.IsDevelopment())
                _logger.LogInformation($"Message: {message.Text}");

            if (message.Text.StartsWith('/')) // Text is a command
            {
                await HandleCommand(message);
                return;
            }

            var chatConfig = await GetChatConfiguration(message.Chat.Id);

            if (chatConfig.IsBotStopped)
                return;

            var grammarService = GetConfiguredGrammarService(chatConfig);

            // Remove emojis, hashtags and mentions
            var text = StringUtils.RemoveEmojis(StringUtils.RemoveHashtags(StringUtils.RemoveMentions(message.Text)));

            var corretionResult = await grammarService.GetCorrections(text);

            if (corretionResult.HasCorrections)
            {
                var messageBuilder = new StringBuilder();

                foreach (var correction in corretionResult.Corrections)
                {
                    var correctionDetailMessage = !chatConfig.HideCorrectionDetails && !string.IsNullOrEmpty(correction.Message)
                        ? $"[{correction.Message}]"
                        : string.Empty;

                    messageBuilder.AppendLine($"*{correction.PossibleReplacements.First()} {correctionDetailMessage}");
                }

                await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString(), replyToMessageId: message.MessageId);
            }
        }

        private async Task<ChatConfiguration> GetChatConfiguration(long chatId)
        {
            var chatConfig = await _chatConfigurationService.GetConfigurationByChatId(chatId);

            if (chatConfig != null)
                return chatConfig;

            var chatConfiguration = new ChatConfiguration
            {
                ChatId = chatId,
                GrammarAlgorithm = Defaults.DefaultAlgorithm,
                SelectedLanguage = SupportedLanguages.Auto
            };

            await _chatConfigurationService.AddConfiguration(chatConfiguration);

            return chatConfiguration;
        }

        private IGrammarService GetConfiguredGrammarService(ChatConfiguration chatConfig)
        {
            var grammarService = _grammarServices.First(v => v.GrammarAlgorith == chatConfig.GrammarAlgorithm);
            grammarService.SetSelectedLanguage(chatConfig.SelectedLanguage);

            return grammarService;
        }

        private async Task HandleCommand(Message message)
        {
            var text = message.Text;

            // TODO: Evaluate moving all this logic into a service, and do a refactor

            if (IsCommand(Commands.Start, text))
            {
                var chatConfig = await _chatConfigurationService.GetConfigurationByChatId(message.Chat.Id);
                var messageBuilder = new StringBuilder();

                if (chatConfig == null)
                {
                    messageBuilder.AppendLine("Hi, I'm GrammarNazi.");
                    messageBuilder.AppendLine("I'm currently working and correcting all spelling errors in this chat.");
                    messageBuilder.AppendLine($"Type {Commands.Help} to get useful commands.");

                    var chatConfiguration = new ChatConfiguration
                    {
                        ChatId = message.Chat.Id,
                        GrammarAlgorithm = Defaults.DefaultAlgorithm,
                        SelectedLanguage = SupportedLanguages.Auto
                    };

                    await _chatConfigurationService.AddConfiguration(chatConfiguration);
                }
                else
                {
                    if (chatConfig.IsBotStopped)
                    {
                        chatConfig.IsBotStopped = false;
                        await _chatConfigurationService.Update(chatConfig);
                        messageBuilder.AppendLine("Bot started");
                    }
                    else
                    {
                        messageBuilder.AppendLine("Bot is already started");
                    }
                }

                await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString());
            }
            else if (IsCommand(Commands.Help, text))
            {
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Help").AppendLine();
                messageBuilder.AppendLine("Useful commands:");
                messageBuilder.AppendLine($"{Commands.Start} start/activate the Bot.");
                messageBuilder.AppendLine($"{Commands.Stop} stop/disable the Bot.");
                messageBuilder.AppendLine($"{Commands.Settings} get configured settings.");
                messageBuilder.AppendLine($"{Commands.SetAlgorithm} <algorithm_number> to set an algorithm.");
                messageBuilder.AppendLine($"{Commands.Language} <language_number> to set a language.");
                messageBuilder.AppendLine($"{Commands.ShowDetails} Show correction details");
                messageBuilder.AppendLine($"{Commands.HideDetails} Hide correction details");

                await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString());
            }
            else if (IsCommand(Commands.Settings, text))
            {
                var chatConfig = await GetChatConfiguration(message.Chat.Id);

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(GetAvailableAlgorithms(chatConfig.GrammarAlgorithm));
                messageBuilder.AppendLine(GetSupportedLanguages(chatConfig.SelectedLanguage));

                var showCorrectionDetailsIcon = chatConfig.HideCorrectionDetails ? "❌" : "✅";
                messageBuilder.AppendLine($"Show correction details {showCorrectionDetailsIcon}");

                if (chatConfig.IsBotStopped)
                    messageBuilder.AppendLine($"The bot is currently stopped. Type {Commands.Start} to activate the Bot.");

                await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString());
            }
            else if (IsCommand(Commands.SetAlgorithm, text))
            {
                var parameters = text.Split(" ");
                if (parameters.Length == 1)
                {
                    var chatConfig = await GetChatConfiguration(message.Chat.Id);

                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"Parameter not received. Type {Commands.SetAlgorithm} <algorithm_numer> to set an algorithm").AppendLine();
                    messageBuilder.AppendLine(GetAvailableAlgorithms(chatConfig.GrammarAlgorithm));
                    await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString());
                }
                else
                {
                    bool parsedOk = int.TryParse(parameters[1], out int algorithm);

                    if (parsedOk)
                    {
                        var chatConfig = await GetChatConfiguration(message.Chat.Id);
                        chatConfig.GrammarAlgorithm = (GrammarAlgorithms)algorithm;

                        // Fire and forget
                        _ = _chatConfigurationService.Update(chatConfig);

                        await _client.SendTextMessageAsync(message.Chat.Id, "Algorithm updated.");
                    }
                    else
                    {
                        await _client.SendTextMessageAsync(message.Chat.Id, $"Invalid parameter. Type {Commands.SetAlgorithm} <algorithm_numer> to set an algorithm.");
                    }
                }
            }
            else if (IsCommand(Commands.Language, text))
            {
                var parameters = text.Split(" ");

                if (parameters.Length == 1)
                {
                    var chatConfig = await GetChatConfiguration(message.Chat.Id);

                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"Parameter not received. Type {Commands.Language} <language_number> to set a language.").AppendLine();
                    messageBuilder.AppendLine(GetSupportedLanguages(chatConfig.SelectedLanguage));
                    await _client.SendTextMessageAsync(message.Chat.Id, messageBuilder.ToString());
                }
                else
                {
                    bool parsedOk = int.TryParse(parameters[1], out int language);

                    if (parsedOk)
                    {
                        var chatConfig = await GetChatConfiguration(message.Chat.Id);
                        chatConfig.SelectedLanguage = (SupportedLanguages)language;

                        // Fire and forget
                        _ = _chatConfigurationService.Update(chatConfig);

                        await _client.SendTextMessageAsync(message.Chat.Id, "Language updated.");
                    }
                    else
                    {
                        await _client.SendTextMessageAsync(message.Chat.Id, $"Invalid parameter. Type {Commands.Language} <language_number> to set a language.");
                    }
                }
            }
            else if (IsCommand(Commands.Stop, text))
            {
                var chatConfig = await GetChatConfiguration(message.Chat.Id);

                chatConfig.IsBotStopped = true;

                // Fire and forget
                _ = _chatConfigurationService.Update(chatConfig);

                await _client.SendTextMessageAsync(message.Chat.Id, $"Bot stopped");
            }
            else if (IsCommand(Commands.HideDetails, text))
            {
                var chatConfig = await GetChatConfiguration(message.Chat.Id);

                chatConfig.HideCorrectionDetails = true;

                // Fire and forget
                _ = _chatConfigurationService.Update(chatConfig);

                await _client.SendTextMessageAsync(message.Chat.Id, "Correction details hidden ✅");
            }
            else if (IsCommand(Commands.ShowDetails, text))
            {
                var chatConfig = await GetChatConfiguration(message.Chat.Id);

                chatConfig.HideCorrectionDetails = false;

                // Fire and forget
                _ = _chatConfigurationService.Update(chatConfig);

                await _client.SendTextMessageAsync(message.Chat.Id, "Show correction details ✅");
            }

            bool IsCommand(string expected, string actual)
            {
                if (actual.Contains("@"))
                {
                    // TODO: Get bot name from config
                    return _webHostEnvironment.IsDevelopment()
                        ? actual.StartsWith($"{expected}@grammarNaziTest_Bot")
                        : actual.StartsWith($"{expected}@grammarNz_Bot");
                }

                return actual.StartsWith(expected);
            }

            static string GetAvailableAlgorithms(GrammarAlgorithms selectedAlgorith)
            {
                var algorithms = Enum.GetValues(typeof(GrammarAlgorithms)).Cast<GrammarAlgorithms>();

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Algorithms available:");

                foreach (var item in algorithms)
                {
                    var selected = item == selectedAlgorith ? "✅" : "";
                    messageBuilder.AppendLine($"{(int)item} - {item.GetDescription()} {selected}");
                }

                return messageBuilder.ToString();
            }

            static string GetSupportedLanguages(SupportedLanguages selectedLanguage)
            {
                var languages = Enum.GetValues(typeof(SupportedLanguages)).Cast<SupportedLanguages>();

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Supported Languages:");

                foreach (var item in languages)
                {
                    var selected = item == selectedLanguage ? "✅" : "";
                    messageBuilder.AppendLine($"{(int)item} - {item} {selected}");
                }

                return messageBuilder.ToString();
            }
        }
    }
}