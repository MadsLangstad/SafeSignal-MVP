using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Prometheus;

namespace SafeSignal.Edge.PaService.Services;

/// <summary>
/// MQTT Subscriber Service for PA Service
/// Listens for PA commands and triggers audio playback
/// </summary>
public class MqttSubscriberService : BackgroundService
{
    private readonly TtsStubService _ttsService;
    private readonly ILogger<MqttSubscriberService> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;

    // Prometheus metrics
    private static readonly Counter PaCommandsTotal = Metrics.CreateCounter(
        "pa_commands_total",
        "Total PA commands received",
        new CounterConfiguration
        {
            LabelNames = new[] { "status" }
        });

    private static readonly Gauge PaPlaybackSuccessRatio = Metrics.CreateGauge(
        "pa_playback_success_ratio",
        "Ratio of successful PA playback operations");

    private static readonly Histogram PaLatency = Metrics.CreateHistogram(
        "pa_command_to_playback_latency_seconds",
        "Time from PA command received to playback completion",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 10)
        });

    private int _commandsReceived = 0;
    private int _playbackSuccesses = 0;

    public MqttSubscriberService(
        TtsStubService ttsService,
        ILogger<MqttSubscriberService> logger,
        IConfiguration configuration)
    {
        _ttsService = ttsService;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PA MQTT Subscriber Service starting...");

        try
        {
            await ConnectToMqttBroker(stoppingToken);

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PA MQTT Subscriber Service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in PA MQTT Subscriber Service");
            throw;
        }
    }

    private async Task ConnectToMqttBroker(CancellationToken stoppingToken)
    {
        var brokerHost = _configuration["Mqtt:BrokerHost"] ?? "emqx";
        var brokerPort = int.Parse(_configuration["Mqtt:BrokerPort"] ?? "8883");
        var clientId = _configuration["Mqtt:ClientId"] ?? "pa-service";
        var certPath = _configuration["Mqtt:ClientCertPath"] ?? "/certs/pa-service/client.crt";
        var keyPath = _configuration["Mqtt:ClientKeyPath"] ?? "/certs/pa-service/client.key";
        var caPath = _configuration["Mqtt:CaCertPath"] ?? "/certs/ca/ca.crt";

        _logger.LogInformation(
            "Connecting to MQTT broker: Host={Host}, Port={Port}, ClientId={ClientId}",
            brokerHost, brokerPort, clientId);

        // Load TLS certificates
        var clientCert = LoadCertificate(certPath, keyPath);
        var caCert = new X509Certificate2(caPath);

        // Configure MQTT client options
        var mqttOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .WithClientId(clientId)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    Certificates = new List<X509Certificate2> { clientCert },
                    CertificateValidationHandler = context => true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls13 | System.Security.Authentication.SslProtocols.Tls12
                })
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                .Build())
            .Build();

        // Create managed MQTT client
        _mqttClient = new MqttFactory().CreateManagedMqttClient();

        // Wire up event handlers
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        // Start client
        await _mqttClient.StartAsync(mqttOptions);

        _logger.LogInformation("MQTT client started successfully");
    }

    private X509Certificate2 LoadCertificate(string certPath, string keyPath)
    {
        try
        {
            var certPem = File.ReadAllText(certPath);
            var keyPem = File.ReadAllText(keyPath);

            return X509Certificate2.CreateFromPem(certPem, keyPem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load TLS certificate: Cert={CertPath}, Key={KeyPath}",
                certPath, keyPath);
            throw;
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("Connected to MQTT broker");

        // Subscribe to PA command topic (wildcard for all rooms)
        var paTopic = "pa/+/play";
        await _mqttClient!.SubscribeAsync(paTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
        _logger.LogInformation("Subscribed to PA command topic: {Topic}", paTopic);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("Received MQTT message: Topic={Topic}, PayloadLength={Length}",
                topic, payload.Length);

            if (topic.StartsWith("pa/") && topic.EndsWith("/play"))
            {
                await HandlePaCommand(topic, payload, receivedAt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message: Topic={Topic}",
                args.ApplicationMessage.Topic);
            PaCommandsTotal.WithLabels("error").Inc();
        }
    }

    private async Task HandlePaCommand(string topic, string payload, DateTimeOffset receivedAt)
    {
        using var _ = PaLatency.NewTimer();

        try
        {
            // Parse PA command
            var command = JsonSerializer.Deserialize<PaCommand>(payload);
            if (command == null)
            {
                _logger.LogError("Failed to deserialize PA command: {Payload}", payload);
                PaCommandsTotal.WithLabels("parse_error").Inc();
                return;
            }

            _commandsReceived++;
            PaCommandsTotal.WithLabels("received").Inc();

            _logger.LogInformation(
                "PA command received: AlertId={AlertId}, Room={Room}, ClipRef={ClipRef}",
                command.AlertId, command.RoomId, command.ClipRef);

            // Get or generate audio
            byte[] audioData;
            if (command.ClipRef == "EMERGENCY_ALERT")
            {
                // Use pre-recorded clip
                audioData = await _ttsService.GetAudioClipAsync(command.ClipRef);
            }
            else
            {
                // Generate TTS
                audioData = await _ttsService.GenerateTtsAsync("Emergency alert. Please follow evacuation procedures.");
            }

            // Play audio on PA system
            var result = await _ttsService.PlayAudioAsync(command.RoomId, audioData);

            // Send acknowledgement
            await SendPaStatus(command, result);

            if (result.Success)
            {
                _playbackSuccesses++;
                PaCommandsTotal.WithLabels("success").Inc();

                // Update success ratio metric
                var ratio = (double)_playbackSuccesses / _commandsReceived;
                PaPlaybackSuccessRatio.Set(ratio);

                _logger.LogInformation(
                    "PA playback completed: AlertId={AlertId}, Room={Room}, LatencyMs={LatencyMs}",
                    command.AlertId, command.RoomId,
                    (DateTimeOffset.UtcNow - receivedAt).TotalMilliseconds);
            }
            else
            {
                PaCommandsTotal.WithLabels("playback_failed").Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PA command: Topic={Topic}", topic);
            PaCommandsTotal.WithLabels("error").Inc();
        }
    }

    private async Task SendPaStatus(PaCommand command, PlaybackResult result)
    {
        var status = new PaStatus
        {
            AlertId = command.AlertId,
            RoomId = command.RoomId,
            Status = result.Success ? "COMPLETED" : "ERROR",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            ErrorMessage = result.ErrorMessage
        };

        var topic = $"pa/{command.RoomId}/status";
        var payload = JsonSerializer.Serialize(status);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttClient!.EnqueueAsync(message);

        _logger.LogDebug("PA status sent: Topic={Topic}, Status={Status}", topic, status.Status);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping PA MQTT Subscriber Service...");

        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync();
            _mqttClient.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}

// DTOs
public class PaCommand
{
    [JsonPropertyName("alertId")]
    public required string AlertId { get; init; }

    [JsonPropertyName("roomId")]
    public required string RoomId { get; init; }

    [JsonPropertyName("clipRef")]
    public required string ClipRef { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("causalChainId")]
    public required string CausalChainId { get; init; }
}

public class PaStatus
{
    [JsonPropertyName("alertId")]
    public required string AlertId { get; init; }

    [JsonPropertyName("roomId")]
    public required string RoomId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
