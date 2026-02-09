using Confluent.Kafka;

namespace Monapi.Worker.Kafka;

// https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#producer
public class KafkaConnector : IDisposable
{
    private IProducer<string, string> producer;
    public KafkaConnector()
    {
        var host = Environment.GetEnvironmentVariable("kafka_server");
        var port = Environment.GetEnvironmentVariable("kafka_port");
        var config = new ProducerConfig
        {
            BootstrapServers = $"{host}:{port}",
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };
        this.producer = new ProducerBuilder<string, string>(config).Build();
    }

    public void WriteMessage(String appName,String message)
    {

        this.producer.Produce("Monarch", new Message<string, string>
        {
            Key = appName, 
            Value = message 
        }, (report) =>
        {
            if (report.Error.IsError)
            {
                Console.WriteLine($"Message Delivery Failed: {report.Error.Reason}");
            }
        });
    }
    public void Dispose()
    {
        this.producer?.Flush(TimeSpan.FromSeconds(5));
        this.producer?.Dispose();
    }
}