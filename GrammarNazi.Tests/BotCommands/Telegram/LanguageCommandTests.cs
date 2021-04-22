﻿using GrammarNazi.Core.BotCommands.Telegram;
using GrammarNazi.Domain.Constants;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using Moq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace GrammarNazi.Tests.BotCommands.Telegram
{
    public class LanguageCommandTests
    {
        [Theory]
        [InlineData("Test")]
        [InlineData("fjkafdk324")]
        public async Task ParameterIsNotNumber_Should_ReplyMessage(string parameter)
        {
            // Arrange
            var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
            var telegramBotClientMock = new Mock<ITelegramBotClient>();
            var command = new LanguageCommand(chatConfigurationServiceMock.Object, telegramBotClientMock.Object);
            const string replyMessage = "Invalid parameter";

            var chatConfig = new ChatConfiguration
            {
                SelectedLanguage = SupportedLanguages.Auto
            };

            var message = new Message
            {
                Text = $"{TelegramBotCommands.Language} {parameter}",
                From = new User { Id = 2 },
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Group
                }
            };

            telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(It.IsAny<ChatId>(), default))
                .ReturnsAsync(new[] { new ChatMember { User = new() { Id = message.From.Id } } });

            telegramBotClientMock.Setup(v => v.GetMeAsync(default))
                .ReturnsAsync(new User { Id = 123456 });

            chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
                .ReturnsAsync(chatConfig);

            // Act
            await command.Handle(message);

            // Assert
            telegramBotClientMock.Verify(v => v.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains(replyMessage)), ParseMode.Default, false, false, 0, null, default));
        }

        [Theory]
        [InlineData("500")]
        [InlineData("123456")]
        public async Task InvalidParameter_Should_ReplyMessage(string parameter)
        {
            // Arrange
            var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
            var telegramBotClientMock = new Mock<ITelegramBotClient>();
            var command = new LanguageCommand(chatConfigurationServiceMock.Object, telegramBotClientMock.Object);
            const string replyMessage = "Invalid parameter";

            var chatConfig = new ChatConfiguration
            {
                SelectedLanguage = SupportedLanguages.Auto
            };

            var message = new Message
            {
                Text = $"{TelegramBotCommands.Language} {parameter}",
                From = new User { Id = 2 },
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Group
                }
            };

            telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(It.IsAny<ChatId>(), default))
                .ReturnsAsync(new[] { new ChatMember { User = new() { Id = message.From.Id } } });

            chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
                .ReturnsAsync(chatConfig);

            telegramBotClientMock.Setup(v => v.GetMeAsync(default))
                .ReturnsAsync(new User { Id = 123456 });

            // Act
            await command.Handle(message);

            // Assert
            telegramBotClientMock.Verify(v => v.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains(replyMessage)), ParseMode.Default, false, false, 0, null, default));
        }

        [Theory]
        [InlineData(SupportedLanguages.English)]
        [InlineData(SupportedLanguages.Spanish)]
        public async Task ValidParameter_Should_ChangeChatConfig_And_ReplyMessage(SupportedLanguages languageParameter)
        {
            // Arrange
            var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
            var telegramBotClientMock = new Mock<ITelegramBotClient>();
            var command = new LanguageCommand(chatConfigurationServiceMock.Object, telegramBotClientMock.Object);
            const string replyMessage = "Language updated";

            var chatConfig = new ChatConfiguration
            {
                SelectedLanguage = SupportedLanguages.Auto
            };

            var message = new Message
            {
                Text = $"{TelegramBotCommands.Language} {(int)languageParameter}",
                From = new User { Id = 2 },
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Group
                }
            };

            telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(It.IsAny<ChatId>(), default))
                .ReturnsAsync(new[] { new ChatMember { User = new() { Id = message.From.Id } } });

            telegramBotClientMock.Setup(v => v.GetMeAsync(default))
                .ReturnsAsync(new User { Id = 123456 });

            chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
                .ReturnsAsync(chatConfig);

            // Act
            await command.Handle(message);

            // Assert
            Assert.Equal(languageParameter, chatConfig.SelectedLanguage);
            telegramBotClientMock.Verify(v => v.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains(replyMessage)), ParseMode.Default, false, false, 0, null, default));
        }

        [Fact]
        public async Task UserNotAdmin_Should_ReplyNotAdminMessage()
        {
            var telegramBotClientMock = new Mock<ITelegramBotClient>();
            await TestUtilities.TestTelegramNotAdminUser(new LanguageCommand(null, telegramBotClientMock.Object), telegramBotClientMock);
        }
    }
}
