﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GrammarNazi.Domain.Constants;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Entities.Settings;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GrammarNazi.App.HostedServices
{
    public class DiscordBotHostedService : BackgroundService
    {
        private readonly BaseSocketClient _client;
        private readonly DiscordSettings _discordSettings;
        private readonly ILogger<DiscordBotHostedService> _logger;
        private readonly IGithubService _githubService;
        private readonly IEnumerable<IGrammarService> _grammarServices;
        private readonly IDiscordChannelConfigService _discordChannelConfigService;
        private readonly IDiscordCommandHandlerService _discordCommandHandlerService;

        public DiscordBotHostedService(BaseSocketClient baseSocketClient,
            IOptions<DiscordSettings> options,
            IGithubService githubService,
            IEnumerable<IGrammarService> grammarServices,
            IDiscordChannelConfigService discordChannelConfigService,
            IDiscordCommandHandlerService discordCommandHandlerService,
            ILogger<DiscordBotHostedService> logger)
        {
            _client = baseSocketClient;
            _discordSettings = options.Value;
            _logger = logger;
            _githubService = githubService;
            _grammarServices = grammarServices;
            _discordChannelConfigService = discordChannelConfigService;
            _discordCommandHandlerService = discordCommandHandlerService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Discord Bot Hosted Service started");

            await _client.LoginAsync(TokenType.Bot, _discordSettings.Token);

            await _client.StartAsync();

            _client.MessageReceived += async (eventArgs) =>
            {
                try
                {
                    await OnMessageReceived(eventArgs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);

                    // fire and forget
                    _ = _githubService.CreateBugIssue($"Application Exception: {ex.Message}", ex);
                }
            };

            // Keep hosted service alive while receiving messages
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task OnMessageReceived(SocketMessage arg)
        {
            if (arg is not SocketUserMessage message || message.Author.IsBot || message.Author.IsWebhook)
                return;

            _logger.LogInformation($"Message received from channel id: {message.Channel.Id}");

            // Text is a command
            if (message.Content.StartsWith(DiscordBotCommands.Prefix))
            {
                await _discordCommandHandlerService.HandleCommand(message);
                return;
            }

            var channelConfig = await GetChatConfiguration(message.Channel.Id);

            if (channelConfig.IsBotStopped)
                return;

            var grammarService = GetConfiguredGrammarService(channelConfig);

            var corretionResult = await grammarService.GetCorrections(message.Content);

            if (!corretionResult.HasCorrections)
                return;

            await message.Channel.TriggerTypingAsync();

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine(message.Author.Mention);

            foreach (var correction in corretionResult.Corrections)
            {
                var correctionDetailMessage = !channelConfig.HideCorrectionDetails && !string.IsNullOrEmpty(correction.Message)
                    ? $"[{correction.Message}]"
                    : string.Empty;

                messageBuilder.AppendLine($"*{correction.PossibleReplacements.First()} {correctionDetailMessage}");
            }

            // TODO: Wait for Discord.NET v2.3.0 release
            //await context.Channel.SendMessageAsync(messageBuilder.ToString(), messageReference: new MessageReference(message.Id));
            await message.Channel.SendMessageAsync(messageBuilder.ToString());
        }

        private IGrammarService GetConfiguredGrammarService(DiscordChannelConfig channelConfig)
        {
            var grammarService = _grammarServices.First(v => v.GrammarAlgorith == channelConfig.GrammarAlgorithm);
            grammarService.SetSelectedLanguage(channelConfig.SelectedLanguage);
            grammarService.SetStrictnessLevel(channelConfig.CorrectionStrictnessLevel);

            return grammarService;
        }

        private async Task<DiscordChannelConfig> GetChatConfiguration(ulong channelId)
        {
            var channelConfig = await _discordChannelConfigService.GetConfigurationByChannelId(channelId);

            if (channelConfig != null)
                return channelConfig;

            var channelConfiguration = new DiscordChannelConfig
            {
                ChannelId = channelId,
                GrammarAlgorithm = Defaults.DefaultAlgorithm,
                SelectedLanguage = SupportedLanguages.Auto
            };

            await _discordChannelConfigService.AddConfiguration(channelConfiguration);

            return channelConfiguration;
        }
    }
}