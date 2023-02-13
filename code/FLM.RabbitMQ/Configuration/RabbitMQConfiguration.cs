namespace FLM.RabbitMQ.Configuration;

public sealed class RabbitMQConfiguration
{
    public required string Host { get; set; }

    public required string Username { get; set; }

    public required string Password { get; set; }

    public int Port { get; set; } = 5672;
}
