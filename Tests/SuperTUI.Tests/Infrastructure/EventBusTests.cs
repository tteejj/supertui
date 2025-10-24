using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SuperTUI.Core;
using SuperTUI.Core.Events;
using Xunit;

namespace SuperTUI.Tests.Infrastructure
{
    public class EventBusTests : IDisposable
    {
        private readonly EventBus eventBus;

        public EventBusTests()
        {
            eventBus = new EventBus();
        }

        public void Dispose()
        {
            eventBus.Clear();
        }

        #region Typed Events Tests

        [Fact]
        public void Subscribe_TypedEvent_ReceivesEvent()
        {
            // Arrange
            TestEvent receivedEvent = null;
            eventBus.Subscribe<TestEvent>(e => receivedEvent = e);

            // Act
            var testEvent = new TestEvent { Message = "Hello" };
            eventBus.Publish(testEvent);

            // Assert
            receivedEvent.Should().NotBeNull();
            receivedEvent.Message.Should().Be("Hello");
        }

        [Fact]
        public void Subscribe_MultipleHandlers_AllReceiveEvent()
        {
            // Arrange
            int handler1Called = 0;
            int handler2Called = 0;

            eventBus.Subscribe<TestEvent>(e => handler1Called++);
            eventBus.Subscribe<TestEvent>(e => handler2Called++);

            // Act
            eventBus.Publish(new TestEvent());

            // Assert
            handler1Called.Should().Be(1);
            handler2Called.Should().Be(1);
        }

        [Fact]
        public void Unsubscribe_TypedEvent_NoLongerReceivesEvents()
        {
            // Arrange
            int callCount = 0;
            Action<TestEvent> handler = e => callCount++;

            eventBus.Subscribe(handler);
            eventBus.Publish(new TestEvent()); // First call

            // Act
            eventBus.Unsubscribe(handler);
            eventBus.Publish(new TestEvent()); // Should not be received

            // Assert
            callCount.Should().Be(1, "handler should only be called once before unsubscribe");
        }

        [Fact]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            // Act
            Action act = () => eventBus.Publish(new TestEvent());

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Priority Tests

        [Fact]
        public void Subscribe_WithPriority_ExecutesInPriorityOrder()
        {
            // Arrange
            var executionOrder = new System.Collections.Generic.List<int>();

            eventBus.Subscribe<TestEvent>(e => executionOrder.Add(1), SubscriptionPriority.Low);
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add(2), SubscriptionPriority.Normal);
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add(3), SubscriptionPriority.High);
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add(4), SubscriptionPriority.Critical);

            // Act
            eventBus.Publish(new TestEvent());

            // Assert
            executionOrder.Should().Equal(4, 3, 2, 1, "should execute in priority order (Critical→High→Normal→Low)");
        }

        #endregion

        #region Weak Reference Tests

        [Fact]
        public void Subscribe_WithWeakReference_CleanedUpWhenGarbageCollected()
        {
            // Arrange
            CreateSubscriberAndCollect();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Act
            eventBus.CleanupDeadSubscriptions();
            var hasSubscribers = eventBus.HasSubscribers<TestEvent>();

            // Assert
            hasSubscribers.Should().BeFalse("weak reference should be garbage collected");
        }

        private void CreateSubscriberAndCollect()
        {
            var subscriber = new TestSubscriber();
            eventBus.Subscribe<TestEvent>(subscriber.Handle, useWeakReference: true);
            // subscriber goes out of scope here
        }

        [Fact]
        public void Subscribe_WithStrongReference_NotCleanedUpAfterGC()
        {
            // Arrange
            CreateStrongSubscriber();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Act
            eventBus.CleanupDeadSubscriptions();
            var hasSubscribers = eventBus.HasSubscribers<TestEvent>();

            // Assert
            hasSubscribers.Should().BeTrue("strong reference should survive GC");
        }

        private void CreateStrongSubscriber()
        {
            var subscriber = new TestSubscriber();
            eventBus.Subscribe<TestEvent>(subscriber.Handle, useWeakReference: false);
        }

        #endregion

        #region Named Events Tests

        [Fact]
        public void Subscribe_NamedEvent_ReceivesEvent()
        {
            // Arrange
            object receivedData = null;
            eventBus.Subscribe("test.event", data => receivedData = data);

            // Act
            eventBus.Publish("test.event", "Hello");

            // Assert
            receivedData.Should().Be("Hello");
        }

        [Fact]
        public void Unsubscribe_NamedEvent_NoLongerReceivesEvents()
        {
            // Arrange
            int callCount = 0;
            Action<object> handler = data => callCount++;

            eventBus.Subscribe("test.event", handler);
            eventBus.Publish("test.event"); // First call

            // Act
            eventBus.Unsubscribe("test.event", handler);
            eventBus.Publish("test.event"); // Should not be received

            // Assert
            callCount.Should().Be(1);
        }

        #endregion

        #region Request/Response Tests

        [Fact]
        public void RegisterRequestHandler_Request_ReturnsResponse()
        {
            // Arrange
            eventBus.RegisterRequestHandler<GetSystemStatsRequest, GetSystemStatsResponse>(req =>
                new GetSystemStatsResponse
                {
                    CpuPercent = 45.0,
                    MemoryUsed = 1024,
                    MemoryTotal = 2048
                });

            // Act
            var response = eventBus.Request<GetSystemStatsRequest, GetSystemStatsResponse>(
                new GetSystemStatsRequest());

            // Assert
            response.Should().NotBeNull();
            response.CpuPercent.Should().Be(45.0);
            response.MemoryUsed.Should().Be(1024);
        }

        [Fact]
        public void Request_NoHandler_ThrowsException()
        {
            // Act
            Action act = () => eventBus.Request<GetSystemStatsRequest, GetSystemStatsResponse>(
                new GetSystemStatsRequest());

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*No handler registered*");
        }

        [Fact]
        public void TryRequest_WithHandler_ReturnsTrue()
        {
            // Arrange
            eventBus.RegisterRequestHandler<GetSystemStatsRequest, GetSystemStatsResponse>(req =>
                new GetSystemStatsResponse { CpuPercent = 50.0 });

            // Act
            var success = eventBus.TryRequest<GetSystemStatsRequest, GetSystemStatsResponse>(
                new GetSystemStatsRequest(), out var response);

            // Assert
            success.Should().BeTrue();
            response.Should().NotBeNull();
            response.CpuPercent.Should().Be(50.0);
        }

        [Fact]
        public void TryRequest_NoHandler_ReturnsFalse()
        {
            // Act
            var success = eventBus.TryRequest<GetSystemStatsRequest, GetSystemStatsResponse>(
                new GetSystemStatsRequest(), out var response);

            // Assert
            success.Should().BeFalse();
            response.Should().BeNull();
        }

        #endregion

        #region Utilities Tests

        [Fact]
        public void HasSubscribers_WithSubscribers_ReturnsTrue()
        {
            // Arrange
            eventBus.Subscribe<TestEvent>(e => { });

            // Act & Assert
            eventBus.HasSubscribers<TestEvent>().Should().BeTrue();
        }

        [Fact]
        public void HasSubscribers_NoSubscribers_ReturnsFalse()
        {
            // Act & Assert
            eventBus.HasSubscribers<TestEvent>().Should().BeFalse();
        }

        [Fact]
        public void GetStatistics_TracksPublishAndDelivery()
        {
            // Arrange
            eventBus.Subscribe<TestEvent>(e => { });
            eventBus.Subscribe<TestEvent>(e => { });

            // Act
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new TestEvent());

            var stats = eventBus.GetStatistics();

            // Assert
            stats.Published.Should().Be(2, "two events were published");
            stats.Delivered.Should().Be(4, "two handlers * two events = 4 deliveries");
            stats.TypedSubscribers.Should().Be(2);
        }

        [Fact]
        public void Clear_RemovesAllSubscriptions()
        {
            // Arrange
            eventBus.Subscribe<TestEvent>(e => { });
            eventBus.Subscribe("named.event", data => { });

            // Act
            eventBus.Clear();

            // Assert
            eventBus.HasSubscribers<TestEvent>().Should().BeFalse();
            eventBus.HasSubscribers("named.event").Should().BeFalse();
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void Publish_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            int totalReceived = 0;
            eventBus.Subscribe<TestEvent>(e => Interlocked.Increment(ref totalReceived));

            // Act - Publish from multiple threads
            Parallel.For(0, 100, i =>
            {
                eventBus.Publish(new TestEvent());
            });

            // Assert
            totalReceived.Should().Be(100, "all events should be delivered despite concurrent access");
        }

        [Fact]
        public void Subscribe_ConcurrentAccess_ThreadSafe()
        {
            // Act - Subscribe from multiple threads
            Parallel.For(0, 50, i =>
            {
                eventBus.Subscribe<TestEvent>(e => { });
            });

            // Assert - Should have 50 subscribers
            var stats = eventBus.GetStatistics();
            stats.TypedSubscribers.Should().Be(50);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Publish_HandlerThrowsException_ContinuesWithOtherHandlers()
        {
            // Arrange
            int handler1Called = 0;
            int handler2Called = 0;

            eventBus.Subscribe<TestEvent>(e =>
            {
                handler1Called++;
                throw new Exception("Handler 1 error");
            });
            eventBus.Subscribe<TestEvent>(e => handler2Called++);

            // Act
            eventBus.Publish(new TestEvent());

            // Assert
            handler1Called.Should().Be(1, "handler 1 should be called despite throwing");
            handler2Called.Should().Be(1, "handler 2 should be called even though handler 1 threw");
        }

        #endregion

        #region Test Classes

        private class TestEvent
        {
            public string Message { get; set; }
        }

        private class TestSubscriber
        {
            public void Handle(TestEvent evt) { }
        }

        #endregion
    }
}
