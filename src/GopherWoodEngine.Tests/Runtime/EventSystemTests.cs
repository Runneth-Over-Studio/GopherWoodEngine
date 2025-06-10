using GopherWoodEngine.Runtime.Modules;
using Microsoft.Extensions.Logging;
using Moq;

namespace GopherWoodEngine.Tests.Runtime;

[TestClass]
public sealed class EventSystemTests
{
    // Method Naming: MethodName_Scenario_ExpectedOutcome
    // Method Structure: Arrange, Act, Assert

    [TestMethod]
    public void Publish_SubscribedHandlerInvokes_HandlerCalledWithCorrectData()
    {
        // Arrange
        Mock<ILogger<IEventSystem>> loggerMock = new Mock<ILogger<IEventSystem>>();
        IEventSystem eventSystem = new EventSystem(loggerMock.Object);
        bool handlerCalled = false;
        int receivedValue = 0;
        TestEventArgs eventArgs = new() { Value = 2024 };
        void handler(object? sender, TestEventArgs args) { handlerCalled = true; receivedValue = args.Value; }
        eventSystem.Subscribe((EventHandler<TestEventArgs>)handler);

        // Act
        eventSystem.Publish(this, eventArgs);

        // Assert
        Assert.IsTrue(handlerCalled, "Handler should have been called.");
        Assert.AreEqual(2024, receivedValue, "Handler should receive correct event data.");
    }

    private class TestEventArgs : EventArgs
    {
        public int Value { get; set; }
    }
}
