using System.Text.Json;
using Confluent.Kafka;

namespace Monapi.Worker.Kafka;


/// <summary>
/// Devon Nelson
/// 
/// This connector will implement the Kafka Producer
/// 
/// 
/// https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#producer
/// </summary>
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
            MessageSendMaxRetries = 5,
            RetryBackoffMs = 150
        };
        this.producer = new ProducerBuilder<string, string>(config).Build();
    }

    public String StatusMap(string statusCode)
    {
        var returnText = statusCode switch
        {
            "0" => "operational",
            "1" => "degraded",
            "2" => "down",
            _ => "unknown"
        };
        return statusCode;
    }

    public void WriteMessage(String appName, String currentStatus, String previousStatus)
    {
        var appData = new
        {
            AppName = appName,
            CurrentStatus = currentStatus,
            CurrentStatusString = StatusMap(currentStatus),
            PreviousStatus = previousStatus,
            PreviousStatusString = StatusMap(previousStatus),
            Timestamp = DateTime.UtcNow

        };

        String jsonAppData = JsonSerializer.Serialize(appData);

        this.producer.Produce("Monarch", new Message<string, string>
        {
            Key = appName,
            Value = jsonAppData
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