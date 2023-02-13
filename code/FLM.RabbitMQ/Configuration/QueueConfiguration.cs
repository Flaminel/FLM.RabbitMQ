using RabbitMQClient = RabbitMQ.Client;

namespace FLM.RabbitMQ.Configuration;

public sealed class QueueConfiguration
{
    public required string QueueName { get; init; }

    public string ExchangeName { get; init; } = "";

    public string ExchangeType { get; init; } = RabbitMQClient.ExchangeType.Direct;

    public string RoutingKey { get; init; } = "";

    public Func<object?, IDictionary<string, object>, Task>? OnMessage { get; init; }

    public Func<Task>? OnQueueIdle { get; init; }

    public ushort BatchSize { get; init; } = 1;

    public ushort PrefetchCount { get; init; } = 1;

    public required Type MessageType { get; init; }

    public ushort? QueueIdleTimeout { get; init; }
}
