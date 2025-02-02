﻿using GrammarNazi.Core.Services;
using GrammarNazi.Domain.BotCommands;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using GrammarNazi.Domain.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace GrammarNazi.Tests.Services;

public class TelegramCommandHandlerServiceTests
{
    [Theory]
    [InlineData("SupportedLanguages.English", SupportedLanguages.English)]
    [InlineData("SupportedLanguages.Spanish", SupportedLanguages.Spanish)]
    public async Task HandleCallBackQuery_LanguageChange_Should_ChangeSelectedLanguage(string callBackQueryData, SupportedLanguages expectedLanguage)
    {
        // Arrange
        var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
        var telegramBotClientMock = new Mock<ITelegramBotClientWrapper>();
        var botCommandsMock = new Mock<IEnumerable<ITelegramBotCommand>>();
        var service = new TelegramCommandHandlerService(chatConfigurationServiceMock.Object, telegramBotClientMock.Object, botCommandsMock.Object);

        var chatConfig = new ChatConfiguration
        {
            SelectedLanguage = SupportedLanguages.Auto
        };

        var message = new Message
        {
            From = new User { Id = 2 },
            Chat = new Chat
            {
                Id = 1,
                Type = ChatType.Group
            }
        };

        var callbackQuery = new CallbackQuery { Message = message, From = message.From, Data = callBackQueryData };

        telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(message.Chat.Id, default))
            .ReturnsAsync(new[] { new ChatMemberMember { User = new() { Id = message.From.Id } } });

        telegramBotClientMock.Setup(v => v.GetMeAsync(default))
            .ReturnsAsync(new User { Id = 123456 });

        chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
            .ReturnsAsync(chatConfig);

        // Act
        await service.HandleCallBackQuery(callbackQuery);

        // Assert
        Assert.Equal(expectedLanguage, chatConfig.SelectedLanguage);
    }

    [Theory]
    [InlineData("GrammarAlgorithms.DatamuseApi", GrammarAlgorithms.DatamuseApi)]
    [InlineData("GrammarAlgorithms.LanguageToolApi", GrammarAlgorithms.LanguageToolApi)]
    [InlineData("GrammarAlgorithms.YandexSpellerApi", GrammarAlgorithms.YandexSpellerApi)]
    [InlineData("GrammarAlgorithms.InternalAlgorithm", GrammarAlgorithms.InternalAlgorithm)]
    public async Task HandleCallBackQuery_AlgorithmChange_Should_ChangeSelectedAlgorithm(string callBackQueryData, GrammarAlgorithms grammarAlgorithm)
    {
        // Arrange
        var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
        var telegramBotClientMock = new Mock<ITelegramBotClientWrapper>();
        var botCommandsMock = new Mock<IEnumerable<ITelegramBotCommand>>();
        var service = new TelegramCommandHandlerService(chatConfigurationServiceMock.Object, telegramBotClientMock.Object, botCommandsMock.Object);

        var chatConfig = new ChatConfiguration
        {
            GrammarAlgorithm = GrammarAlgorithms.InternalAlgorithm
        };

        var message = new Message
        {
            From = new User { Id = 2 },
            Chat = new Chat
            {
                Id = 1,
                Type = ChatType.Group
            }
        };

        var callbackQuery = new CallbackQuery { Message = message, From = message.From, Data = callBackQueryData };

        telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(message.Chat.Id, default))
            .ReturnsAsync(new[] { new ChatMemberMember { User = new() { Id = message.From.Id } } });

        telegramBotClientMock.Setup(v => v.GetMeAsync(default))
            .ReturnsAsync(new User { Id = 123456 });

        chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
            .ReturnsAsync(chatConfig);

        // Act
        await service.HandleCallBackQuery(callbackQuery);

        // Assert
        Assert.Equal(grammarAlgorithm, chatConfig.GrammarAlgorithm);
    }

    [Fact]
    public async Task HandleCallBackQuery_UserNotAdmin_Should_ReplyMessage()
    {
        // Arrange
        var chatConfigurationServiceMock = new Mock<IChatConfigurationService>();
        var telegramBotClientMock = new Mock<ITelegramBotClientWrapper>();
        var botCommandsMock = new Mock<IEnumerable<ITelegramBotCommand>>();
        var service = new TelegramCommandHandlerService(chatConfigurationServiceMock.Object, telegramBotClientMock.Object, botCommandsMock.Object);
        const string replyMessage = "Only admins can use this command.";

        var chatConfig = new ChatConfiguration
        {
            GrammarAlgorithm = GrammarAlgorithms.InternalAlgorithm
        };

        var message = new Message
        {
            From = new User { Id = 2, FirstName = "User" },
            Chat = new Chat
            {
                Id = 1,
                Type = ChatType.Group
            }
        };

        var callbackQuery = new CallbackQuery { Message = message, From = message.From, Data = "" };

        telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(message.Chat.Id, default))
            .ReturnsAsync(new[] { new ChatMemberMember { User = new() { Id = 100 } } });

        telegramBotClientMock.Setup(v => v.GetMeAsync(default))
            .ReturnsAsync(new User { Id = 123456 });

        chatConfigurationServiceMock.Setup(v => v.GetConfigurationByChatId(message.Chat.Id))
            .ReturnsAsync(chatConfig);

        // Act
        await service.HandleCallBackQuery(callbackQuery);

        // Assert
        telegramBotClientMock.Verify(v => v.SendTextMessageAsync(message.Chat.Id, It.Is<string>(s => s.Contains(replyMessage)), ParseMode.Markdown, default, default, default, default, default, default, default, default));
    }

    [Theory]
    [InlineData("/start@botTest")]
    [InlineData("/stop@botUsername")]
    [InlineData("/settings@botUsername")]
    public async Task CommandForAnotherBot_Should_Not_DoAnything(string command)
    {
        // Arrange
        var telegramBotClientMock = new Mock<ITelegramBotClientWrapper>();
        var service = new TelegramCommandHandlerService(null, telegramBotClientMock.Object, GetAllCommands());

        var message = new Message
        {
            Text = command,
            From = new User { Id = 2 },
            Chat = new Chat
            {
                Id = 1,
                Type = ChatType.Group
            }
        };

        telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(message.Chat.Id, default))
            .ReturnsAsync(new[] { new ChatMemberMember { User = new() { Id = message.From.Id } } });

        // Act
        await service.HandleCommand(message);

        // Assert

        // Make sure SendTextMessageAsync method was never called
        telegramBotClientMock.Verify(v => v.SendTextMessageAsync(message.Chat.Id, It.IsAny<string>(), default, default, default, default, default, default, default, default, default), Times.Never);
    }

    [Theory]
    [InlineData("/test_command")]
    [InlineData("/command")]
    [InlineData("/bot_command")]
    [InlineData("/another_command")]
    public async Task UnknownCommand_Should_Not_DoAnything(string command)
    {
        // Arrange
        var telegramBotClientMock = new Mock<ITelegramBotClientWrapper>();
        var service = new TelegramCommandHandlerService(null, telegramBotClientMock.Object, GetAllCommands());

        var message = new Message
        {
            Text = command,
            From = new User { Id = 2 },
            Chat = new Chat
            {
                Id = 1,
                Type = ChatType.Group
            }
        };

        telegramBotClientMock.Setup(v => v.GetChatAdministratorsAsync(message.Chat.Id, default))
            .ReturnsAsync(new[] { new ChatMemberMember { User = new() { Id = message.From.Id } } });

        // Act
        await service.HandleCommand(message);

        // Assert

        // Make sure SendTextMessageAsync method was never called
        telegramBotClientMock.Verify(v => v.SendTextMessageAsync(message.Chat.Id, It.IsAny<string>(), default, default, default, default, default, default, default, default, default), Times.Never);
    }

    private IEnumerable<ITelegramBotCommand> GetAllCommands()
    {
        var commandClassTypes = Assembly.GetExecutingAssembly()
                                .GetReferencedAssemblies()
                                .SelectMany(v => Assembly.Load(v).GetTypes())
                                .Where(v => !v.IsAbstract && v.IsAssignableTo(typeof(ITelegramBotCommand)));

        foreach (var item in commandClassTypes)
        {
            var constructorParameters = item.GetConstructors().First().GetParameters();

            var command = (ITelegramBotCommand)Activator.CreateInstance(item, new object[constructorParameters.Length]);

            yield return command;
        }
    }
}
