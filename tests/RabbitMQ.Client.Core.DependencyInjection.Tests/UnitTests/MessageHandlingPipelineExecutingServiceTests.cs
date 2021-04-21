using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using RabbitMQ.Client.Core.DependencyInjection.Middlewares;
using RabbitMQ.Client.Core.DependencyInjection.Models;
using RabbitMQ.Client.Core.DependencyInjection.Services;
using RabbitMQ.Client.Core.DependencyInjection.Services.Interfaces;
using RabbitMQ.Client.Core.DependencyInjection.Tests.Stubs;
using RabbitMQ.Client.Events;
using Xunit;

namespace RabbitMQ.Client.Core.DependencyInjection.Tests.UnitTests
{
    public class MessageHandlingPipelineExecutingServiceTests
    {
        [Fact]
        public async Task ShouldProperlyExecutePipelineWithNoAdditionalMiddlewares()
        {
            var argsMock = new Mock<BasicDeliverEventArgs>();
            var messageHandlingServiceMock = new Mock<IMessageHandlingService>();

            var service = CreateService(
                messageHandlingServiceMock.Object,
                Enumerable.Empty<IMessageHandlingMiddleware>());

            await service.Execute(argsMock.Object, AckAction);
            messageHandlingServiceMock.Verify(x => x.HandleMessageReceivingEvent(It.IsAny<MessageHandlingContext>()), Times.Once);
        }

        [Fact]
        public async Task ShouldProperlyExecutePipeline()
        {
            var argsMock = new Mock<BasicDeliverEventArgs>();
            var messageHandlingServiceMock = new Mock<IMessageHandlingService>();

            var middlewareOrderingMap = new Dictionary<int, int>();
            var firstMiddleware = new StubMessageHandlingMiddleware(1, middlewareOrderingMap, new Dictionary<int, int>());
            var secondMiddleware = new StubMessageHandlingMiddleware(2, middlewareOrderingMap, new Dictionary<int, int>());
            var thirdMiddleware = new StubMessageHandlingMiddleware(3, middlewareOrderingMap, new Dictionary<int, int>());
            var middlewares = new List<IMessageHandlingMiddleware>
            {
                firstMiddleware,
                secondMiddleware,
                thirdMiddleware
            };

            var service = CreateService(
                messageHandlingServiceMock.Object,
                middlewares);
            await service.Execute(argsMock.Object, AckAction);
            
            messageHandlingServiceMock.Verify(x => x.HandleMessageReceivingEvent(It.IsAny<MessageHandlingContext>()), Times.Once);
            Assert.Equal(1, middlewareOrderingMap[thirdMiddleware.Number]);
            Assert.Equal(2, middlewareOrderingMap[secondMiddleware.Number]);
            Assert.Equal(3, middlewareOrderingMap[firstMiddleware.Number]);
        }

        [Fact]
        public async Task ShouldProperlyExecuteFailurePipelineWhenMessageHandlingServiceThrowsException()
        {
            var argsMock = new Mock<BasicDeliverEventArgs>();
            var exception = new Exception();
            var messageHandlingServiceMock = new Mock<IMessageHandlingService>();
            messageHandlingServiceMock.Setup(x => x.HandleMessageReceivingEvent(It.IsAny<MessageHandlingContext>()))
                .ThrowsAsync(exception);

            var middlewareOrderingMap = new Dictionary<int, int>();
            var firstMiddleware = new StubMessageHandlingMiddleware(1, new Dictionary<int, int>(), middlewareOrderingMap);
            var secondMiddleware = new StubMessageHandlingMiddleware(2, new Dictionary<int, int>(), middlewareOrderingMap);
            var thirdMiddleware = new StubMessageHandlingMiddleware(3, new Dictionary<int, int>(), middlewareOrderingMap);
            var middlewares = new List<IMessageHandlingMiddleware>
            {
                firstMiddleware,
                secondMiddleware,
                thirdMiddleware
            };
            
            var service = CreateService(
                messageHandlingServiceMock.Object,
                middlewares);
            await service.Execute(argsMock.Object, AckAction);
            
            messageHandlingServiceMock.Verify(x => x.HandleMessageProcessingFailure(It.IsAny<MessageHandlingContext>(), exception), Times.Once);
            Assert.Equal(1, middlewareOrderingMap[thirdMiddleware.Number]);
            Assert.Equal(2, middlewareOrderingMap[secondMiddleware.Number]);
            Assert.Equal(3, middlewareOrderingMap[firstMiddleware.Number]);
        }

        private static IMessageHandlingPipelineExecutingService CreateService(
            IMessageHandlingService messageHandlingService,
            IEnumerable<IMessageHandlingMiddleware> middlewares) =>
            new MessageHandlingPipelineExecutingService(messageHandlingService, middlewares);

        private static void AckAction(BasicDeliverEventArgs message) { }
    }
}