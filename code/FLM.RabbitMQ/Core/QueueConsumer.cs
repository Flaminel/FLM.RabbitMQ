using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using FLM.RabbitMQ.Exceptions;

namespace FLM.RabbitMQ.Core;

internal sealed class QueueConsumer: IDisposable
{
    private readonly ILogger _logger;
    private readonly IModel _channel;
    private readonly string _queueName;
    private readonly ushort _batchSize;
    private readonly ushort _prefetchCount;
    private readonly Type _messageType;
    private readonly int _queueIdleTimeout;
    private readonly object _lockObject = new();
    private EventingBasicConsumer? _consumer;
    private bool _consuming;
    private string? _consumerTag;
    private System.Timers.Timer? _idleTimer;

    private const int IDLE_TIMER_INTERVAL = 30_000;

    public required Func<object?, IDictionary<string, object>, Task> OnMessage { get; init; }

    public Func<Task>? OnQueueIdle { get; init; }

    public QueueConsumer(
        ILogger logger,
        IModel channel,
        string queueName,
        ushort batchSize,
        ushort prefetchCount,
        Type messageType,
        ushort? queueIdleTimeout)
    {
        _logger = logger;
        _channel = channel;
        _queueName = queueName;
        _batchSize = batchSize;
        _prefetchCount = prefetchCount;
        _messageType = messageType;

        _queueIdleTimeout = queueIdleTimeout is null 
            ? IDLE_TIMER_INTERVAL
            : queueIdleTimeout.Value;

        Validate();
    }

    public void StartConsuming()
    {
        if (_consuming || OnMessage is null)
        {
            return;
        }

        // Set the QoS settings for the consumer channel
        _channel.BasicQos(0, _prefetchCount, false);

        _consumer = new EventingBasicConsumer(_channel);
        _consumer.Received += OnReceived;

        _consumerTag = _channel.BasicConsume(_queueName, false, _consumer);
        _consuming = true;

        StartIdleTimer();
    }

    public void StopConsuming()
    {
        if (!_consuming)
        {
            return;
        }

        _consuming = false;
        _channel.BasicCancel(_consumerTag);
    }

    public void Dispose()
    {
        _idleTimer?.Dispose();
        StopConsuming();
        GC.SuppressFinalize(this);
    }

    private void StartIdleTimer()
    {
        if (_idleTimer is null)
        {
            _idleTimer = new()
            {
                AutoReset = false,
                Interval = _queueIdleTimeout
            };

            _idleTimer.Elapsed += async (obj, args) =>
            {
                if (OnQueueIdle is null)
                {
                    return;
                }

                _logger.LogInformation("Received queue idle event");
                await OnQueueIdle();
            };
        }
        
        lock (_lockObject)
        {
            if (!_idleTimer.Enabled)
            {
                _idleTimer.Start();
            }
        }
    }

    private void StopIdleTimer()
    {
        lock(_lockObject)
        {
            if (_idleTimer?.Enabled ?? false)
            {
                _idleTimer?.Stop();
            }
        }
    }

    private async void OnReceived(object? sender, BasicDeliverEventArgs e)
    {
        StopIdleTimer();

        if (_consuming)
        {
            byte[] message = e.Body.ToArray();

            try
            {
                object? messageObject = JsonSerializer.Deserialize(Encoding.UTF8.GetString(message), _messageType);
                await OnMessage(messageObject, e.BasicProperties.Headers);
            }
            catch (Exception exception) 
            {
                _logger.LogCritical(
                    exception,
                    """Failed to process message "{Message}" on queue {Queue}""",
                    Encoding.UTF8.GetString(message),
                    _queueName);
            }

            _channel.BasicAck(e.DeliveryTag, false);
        }

        StartIdleTimer();
    }

    private void Validate()
    {
        const string queueIdleTimeoutMessage = "Queue idle timeout can not be less than 10 seconds or more than 2 minutes";

        if (_queueIdleTimeout is < 10_000 or > 120_000)
        {
            _logger.LogError(queueIdleTimeoutMessage);
            throw new ValidationException(queueIdleTimeoutMessage);
        }
    }
}
