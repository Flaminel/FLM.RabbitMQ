using FLM.RabbitMQ.Configuration;

namespace FLM.RabbitMQ.Core.Interfaces;

public interface IRabbitMQConnection: IDisposable
{
    /// <summary>
    /// Configures the RabbitMQ queues specified in the provided queue configurations.
    /// This includes creating the queue and exchange (if specified), and binding the queue to the exchange with the specified routing key.
    /// It also configures a consumer for each queue, with the provided message handling function and prefetch count.
    /// </summary>
    /// <param name="queueConfigurations">The configurations for the queues to be set up</param>
    void ConfigureQueues(IEnumerable<QueueConfiguration> queueConfigurations);

    /// <summary>
    /// Starts to consume messages from the configured queues.
    /// </summary>
    void StartConsuming();

    /// <summary>
    /// Stops the message consuming.
    /// </summary>
    void StopConsuming();

    /// <summary>
    /// Publishes a message with a byte array body to the specified queue, using the specified exchange.
    /// If the exchange name is not provided, the default exchange will be used.
    /// </summary>
    /// <param name="routingKey">The routing key or the name of the queue to publish the message to</param>
    /// <param name="messageBody">The byte array containing the message body</param>
    /// <param name="exchangeName">The name of the exchange to publish the message to (optional, default value is an empty string)</param>
    void PublishMessage(string routingKey, byte[] messageBody, string exchangeName = "");

    /// <summary>
    /// Publishes a message with a string body to the specified queue, using the specified exchange.
    /// If the exchange name is not provided, the default exchange will be used.
    /// </summary>
    /// <param name="routingKey">The routing key or the name of the queue to publish the message to</param>
    /// <param name="messageBody">The string containing the message body</param>
    /// <param name="exchangeName">The name of the exchange to publish the message to (optional, default value is an empty string)</param>
    void PublishMessage(string routingKey, string messageBody, string exchangeName = "");

    /// <summary>
    /// Publishes a message with a serialized object as body to the specified queue, using the specified exchange.
    /// If the exchange name is not provided, the default exchange will be used.
    /// </summary>
    /// <param name="routingKey">The routing key or the name of the queue to publish the message to</param>
    /// <param name="messageBody">The object containing the message body</param>
    /// <param name="exchangeName">The name of the exchange to publish the message to (optional, default value is an empty string)</param>
    void PublishMessage(string routingKey, object messageBody, string exchangeName = "");
}
