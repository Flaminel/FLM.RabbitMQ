using FLM.RabbitMQ.Configuration;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using FLM.RabbitMQ.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FLM.RabbitMQ.Core;

public sealed class RabbitMQConnection : IRabbitMQConnection
{
    private readonly ILogger _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ConcurrentBag<QueueConsumer> _consumers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQConnection"/> class using a configuration object.
    /// A new connection to RabbitMQ will be created using the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration object to use</param>
    public RabbitMQConnection(
        ILogger<RabbitMQConnection> logger,
        IOptions<RabbitMQConfiguration> configuration)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration?.Value, nameof(configuration));

        _logger = logger;
        RabbitMQConfiguration config = configuration.Value;
        _connection = new ConnectionFactory
        {
            HostName = config.Host,
            UserName = config.Username,
            Password = config.Password,
            Port = config.Port,
            AutomaticRecoveryEnabled = true
        }.CreateConnection();
        _channel = _connection.CreateModel();
    }

    /// <inheritdoc/>
    public void ConfigureQueues(IEnumerable<QueueConfiguration> queueConfigurations)
    {
        foreach (QueueConfiguration queueConfig in queueConfigurations)
        {
            ConfigureQueue(
                queueConfig.QueueName,
                queueConfig.ExchangeName,
                queueConfig.ExchangeType,
                queueConfig.RoutingKey);
            ConfigureConsumer(
                queueConfig.QueueName,
                queueConfig.OnMessage,
                queueConfig.OnQueueIdle,
                queueConfig.BatchSize,
                queueConfig.PrefetchCount,
                queueConfig.MessageType,
                queueConfig.QueueIdleTimeout);
        }
    }

    /// <inheritdoc/>
    public void StartConsuming()
    {
        foreach (QueueConsumer consumer in _consumers)
        {
            consumer.StartConsuming();
        }
    }

    /// <inheritdoc/>
    public void StopConsuming()
    {
        foreach (QueueConsumer consumer in _consumers)
        {
            consumer.StopConsuming();
        }
    }

    /// <inheritdoc/>
    public void PublishMessage(string routingKey, byte[] messageBody, string exchangeName = "")
    {
        _channel.BasicPublish(exchangeName, routingKey, null, messageBody);
    }

    /// <inheritdoc/>
    public void PublishMessage(string routingKey, string messageBody, string exchangeName = "")
    {
        PublishMessage(routingKey, Encoding.UTF8.GetBytes(messageBody), exchangeName);
    }

    /// <inheritdoc/>
    public void PublishMessage(string routingKey, object messageBody, string exchangeName = "")
    {
        PublishMessage(routingKey, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageBody)), exchangeName);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();

        foreach (QueueConsumer consumer in _consumers)
        {
            consumer.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Configures a queue, exchange, and binding based on the provided queue name, exchange name and routing key.
    /// If no exchange name is provided, the default exchange will be used.
    /// </summary>
    /// <param name="queueName">The name of the queue to be created or used</param>
    /// <param name="exchangeName">The name of the exchange to be created or used (optional, default value is an empty string)</param>
    /// <param name="exchangeType">The type of exchange to be created or used</param>
    /// <param name="routingKey">The routing key to be used for binding the queue to the exchange</param>
    private void ConfigureQueue(
        string queueName,
        string exchangeName = "",
        string exchangeType = ExchangeType.Direct,
        string? routingKey = null)
    {
        _ = _channel.QueueDeclare(queueName, true, false, false);

        // If an exchange name was provided, declare the exchange and bind the queue to it with the specified routing key
        if (!string.IsNullOrEmpty(exchangeName))
        {
            exchangeType = string.IsNullOrEmpty(exchangeType)
                ? ExchangeType.Direct
                : exchangeType;
            _channel.ExchangeDeclare(exchangeName, exchangeType);
        }

        if (!string.IsNullOrEmpty(exchangeName) || !string.IsNullOrEmpty(routingKey))
        {
            _channel.QueueBind(queueName, exchangeName ?? string.Empty, routingKey);
        }
    }

    /// <summary>
    /// Configures a consumer for the specified queue, with the provided message handling delegate, batch size and prefetch count.
    /// If a consumer for the specified queue has already been created, it will be overwritten.
    /// </summary>
    /// <param name="queueName">The name of the queue to configure the consumer for</param>
    /// <param name="onMessage">The delegate to handle received messages</param>
    /// <param name="batchSize">The number of messages to handle in a batch (optional, default value is 1)</param>
    /// <param name="prefetchCount">The maximum number of unacknowledged messages allowed (optional, default value is 1)</param>
    private void ConfigureConsumer(
        string queueName,
        Func<object?, IDictionary<string, object>, Task>? onMessage,
        Func<Task>? onQueueIdle,
        ushort batchSize,
        ushort prefetchCount,
        Type messageType,
        ushort? queueIdleTimeout)
    {
        if (onMessage is null)
        {
            return;
        }

        // Create a new consumer for the queue, with the provided message handling delegate
        QueueConsumer consumer = new(
            _logger,
            _channel,
            queueName,
            batchSize,
            prefetchCount,
            messageType,
            queueIdleTimeout)
        {
            OnMessage = onMessage,
            OnQueueIdle = onQueueIdle
        };

        _consumers.Add(consumer);
    }
}
