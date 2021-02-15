﻿using Discord;
using Discord.Net;
using Discord.WebSocket;
using GrammarNazi.Core.Utilities;
using GrammarNazi.Domain.Constants;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Entities.Settings;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using Markdig;
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
                catch (HttpException ex) when (ex.Message.Contains("50013"))
                {
                    _logger.LogWarning(ex, "Missing Permissions");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);

                    // fire and forget
                    _ = _githubService.CreateBugIssue($"Application Exception: {ex.Message}", ex, GithubIssueLabels.Discord);
                }
            };

            // Keep hosted service alive while receiving messages
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task OnMessageReceived(SocketMessage socketMessage)
        {
            if (socketMessage is not SocketUserMessage message || message.Author.IsBot || message.Author.IsWebhook)
                return;

            _logger.LogInformation($"Message received from channel id: {message.Channel.Id}");

            var channelConfig = await GetChatConfiguration(message);

            // Text is a command
            if (message.Content.StartsWith(DiscordBotCommands.Prefix))
            {
                await _discordCommandHandlerService.HandleCommand(message);
                return;
            }

            if (channelConfig.IsBotStopped)
                return;

            var grammarService = GetConfiguredGrammarService(channelConfig);

            var text = GetCleannedText(message.Content);

            var corretionResult = await grammarService.GetCorrections(text);

            if (!corretionResult.HasCorrections)
                return;

            await message.Channel.TriggerTypingAsync();

            var messageBuilder = new StringBuilder();

            foreach (var correction in corretionResult.Corrections)
            {
                var correctionDetailMessage = !channelConfig.HideCorrectionDetails && !string.IsNullOrEmpty(correction.Message)
                    ? $"[{correction.Message}]"
                    : string.Empty;

                messageBuilder.AppendLine($"*{correction.PossibleReplacements.First()} {correctionDetailMessage}");
            }

            await message.Channel.SendMessageAsync(messageBuilder.ToString(), messageReference: new MessageReference(message.Id));
        }

        private IGrammarService GetConfiguredGrammarService(DiscordChannelConfig channelConfig)
        {
            var grammarService = _grammarServices.First(v => v.GrammarAlgorith == channelConfig.GrammarAlgorithm);
            grammarService.SetSelectedLanguage(channelConfig.SelectedLanguage);
            grammarService.SetStrictnessLevel(channelConfig.CorrectionStrictnessLevel);

            return grammarService;
        }

        private static string GetCleannedText(string text)
        {
            // TODO: Move ToPlainText to StringUtils
            return Markdown.ToPlainText(StringUtils.RemoveCodeBlocks(text));
        }

        private async Task<DiscordChannelConfig> GetChatConfiguration(SocketUserMessage message)
        {
            var channelConfig = await _discordChannelConfigService.GetConfigurationByChannelId(message.Channel.Id);

            if (channelConfig != null)
                return channelConfig;

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Hi, I'm GrammarNazi.");
            messageBuilder.AppendLine("I'm currently working and correcting all spelling errors in this channel.");
            messageBuilder.AppendLine($"Type `{DiscordBotCommands.Help}` to get useful commands.");

            ulong guild = message.Channel switch
            {
                SocketDMChannel dmChannel => dmChannel.Id,
                SocketGuildChannel guildChannel => guildChannel.Guild.Id,
                _ => default
            };

            var channelConfiguration = new DiscordChannelConfig
            {
                ChannelId = message.Channel.Id,
                GrammarAlgorithm = Defaults.DefaultAlgorithm,
                Guild = guild,
                SelectedLanguage = SupportedLanguages.Auto
            };

            await _discordChannelConfigService.AddConfiguration(channelConfiguration);

            await message.Channel.SendMessageAsync(messageBuilder.ToString());

            return channelConfiguration;
        }
    }
}