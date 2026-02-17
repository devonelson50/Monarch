using System.Text.Json;
using Confluent.Kafka;

namespace Monapi.Worker.Kafka;


/// <summary>
/// Devon Nelson
/// 
/// This object implements a Kafka Producer to forward application status data to
/// a Kafka Broker. Apps are identified by name, and provide status data in a JSON
/// format. The current output is the current and previous status, and the message's 
/// timestamp.
/// 
/// https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#producer
/// https://oneuptime.com/blog/post/2026-02-02-kafka-security-sasl-ssl/view
/// </summary>
public class KafkaConnector : IDisposable
{
    private IProducer<string, string> producer;
    public KafkaConnector()
    {
        Console.WriteLine("Kafka configuration detected. Preparing connection...");
        // Retrieve configuration from environment variables, and Docker Secret
        //
        // Any below value being null or empty will cause an exception to be thrown. This
        // is the intended behavior as the object is only created if the kafka_server variable
        // has been manually populated. Any other missing variables is indicative of a misconfiguration.
        var host = Environment.GetEnvironmentVariable("kafka_server");
        var port = Environment.GetEnvironmentVariable("kafka_port");
        var user = Environment.GetEnvironmentVariable("kafka_user");
        var password = File.ReadAllText("/run/secrets/monarch_kafka_password").Trim();

        // Prepare the producer object
        var config = new ProducerConfig
        {
            // Connection configuration
            BootstrapServers = $"{host}:{port}",
            Acks = Acks.All,
            MessageSendMaxRetries = 5,
            RetryBackoffMs = 150,

            // SecurityProtocol.SaslSsl is needed for safe use in production. This implementation
            // assumes the Kafka broker will present a certificate signed by a publicly trusted CA.
            // If the broker is using a self signed certificate, the SslCaLocation must be defined.
            SecurityProtocol = SecurityProtocol.SaslSsl, // Recommended for production, assumes valid cert
            // SecurityProtocol = SecurityProtocol.SaslPlaintext, // Recommended for development only

            // Authentication details
            SaslMechanism = SaslMechanism.ScramSha256,
            SaslUsername = user,
            SaslPassword = password
        };
        this.producer = new ProducerBuilder<string, string>(config).Build();
    }
    /// <summary>
    /// This maps numeric status to a written word. Will allow construction 
    /// of messages in Kafka such as "DNS-01 is now operational, was previously degraded"
    /// </summary>
    /// <param name="statusCode">Numeric status code as a string</param>
    /// <returns>Written status</returns>
    public String StatusMap(string statusCode)
    {
        var returnText = statusCode switch
        {
            "0" => "operational",
            "1" => "degraded",
            "2" => "down",
            _ => "unknown"
        };
        return returnText;
    }
    /// <summary>
    /// Uses the producer to prepare and send a message to the Kafka Broker.
    /// </summary>
    /// <param name="appName">Name of Monitored App</param>
    /// <param name="currentStatus">Numeric status code as string</param>
    /// <param name="previousStatus">Numeric status code as string</param>
    public void WriteMessage(String appName, String currentStatus, String previousStatus)
    {
        // Prepare JSON
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

        // Submit message to broker
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
    /// <summary>
    /// Clean-up on exit, ensure all queued messages attempt to send.
    /// </summary>
    public void Dispose()
    {
        this.producer?.Flush(TimeSpan.FromSeconds(5));
        this.producer?.Dispose();
    }
}