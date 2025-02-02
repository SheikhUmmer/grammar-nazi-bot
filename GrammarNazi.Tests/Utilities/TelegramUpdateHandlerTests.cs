﻿using GrammarNazi.Core.Utilities;
using GrammarNazi.Domain.Entities;
using GrammarNazi.Domain.Enums;
using GrammarNazi.Domain.Services;
using GrammarNazi.Domain.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Xunit;

namespace GrammarNazi.Tests.Utilities;

public class TelegramUpdateHandlerTests
{
    [Fact]
    public async Task HandleUpdate_NonSupportedUpdateTypeReceived_Should_DoNothing()
    {
        // Arrange
        var telegramBotMock = new Mock<ITelegramBotClient>();
        var loggerMock = new Mock<ILogger<TelegramUpdateHandler>>();

        var update = new Update
        {
            ChatMember = new ChatMemberUpdated()
        };

        var handler = new TelegramUpdateHandler(null, null, loggerMock.Object);

        // Act
        await handler.HandleUpdateAsync(telegramBotMock.Object, update, default);

        // Assert

        // Verify LogInformation was called
        loggerMock.Verify(x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.IsAny<It.IsAnyType>(),
                        It.IsAny<Exception>(),
                        (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));
    }

    [Fact]
    public async Task HandleUpdate_MessageReceivedNotTextType_Should_DoNothing()
    {
        // Arrange
        var telegramBotMock = new Mock<ITelegramBotClient>();
        var chatConfigServiceMock = new Mock<IChatConfigurationService>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();

        var update = new Update
        {
            Message = new Message
            {
                Audio = new Audio(),
                Chat = new Chat { Id = 1 }
            }
        };

        serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
        serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IChatConfigurationService))).Returns(chatConfigServiceMock.Object);

        var handler = new TelegramUpdateHandler(serviceScopeFactory.Object, null, null);

        // Act
        await handler.HandleUpdateAsync(telegramBotMock.Object, update, default);

        // Assert
        chatConfigServiceMock.Verify(x => x.GetConfigurationByChatId(update.Message.Chat.Id), Times.Never);
    }

    [Fact]
    public async Task HandleUpdate_MessageReceived_Should_GetCorrections()
    {
        // Arrange
        var telegramBotMock = new Mock<ITelegramBotClient>();
        var chatConfigServiceMock = new Mock<IChatConfigurationService>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        var loggerMock = new Mock<ILogger<TelegramUpdateHandler>>();
        var grammarService = new Mock<IGrammarService>();

        var update = new Update
        {
            Message = new Message
            {
                Text = "My Text",
                Chat = new Chat { Id = 1 }
            }
        };

        chatConfigServiceMock.Setup(x => x.GetConfigurationByChatId(update.Message.Chat.Id))
            .ReturnsAsync(new ChatConfiguration());

        grammarService.Setup(x => x.GetCorrections("My Text"))
            .ReturnsAsync(new GrammarCheckResult(null));

        serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
        serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IChatConfigurationService))).Returns(chatConfigServiceMock.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IEnumerable<IGrammarService>))).Returns(new[] { grammarService.Object });

        var handler = new TelegramUpdateHandler(serviceScopeFactory.Object, null, loggerMock.Object);

        // Act
        await handler.HandleUpdateAsync(telegramBotMock.Object, update, default);

        // Assert
        chatConfigServiceMock.Verify(x => x.GetConfigurationByChatId(update.Message.Chat.Id));
        grammarService.Verify(x => x.GetCorrections("My Text"));
    }
}
