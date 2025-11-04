using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using SafeSignal.Edge.PolicyService.Models;
using Prometheus;

namespace SafeSignal.Edge.PolicyService.Services;

/// <summary>
/// MQTT Handler Service - Manages MQTT connections and message routing
/// Subscribes to alert triggers, publishes PA commands
/// </summary>
public class MqttHandlerService : BackgroundService
{
    private readonly AlertStateMachine _stateMachine;
    private readonly RateLimitService _rateLimitService;
    private readonly ILogger<MqttHandlerService> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;

    // Prometheus metrics
    private static readonly Counter MqttMessagesTotal = Metrics.CreateCounter(
        "mqtt_messages_total",
        "Total MQTT messages processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "status" }
        });

    private static readonly Gauge PaPlaybackSuccessRatio = Metrics.CreateGauge(
        "pa_playback_success_ratio",
        "Ratio of successful PA playback commands");

    private int _paCommandsSent = 0;
    private int _paAcksReceived = 0;

    public MqttHandlerService(
        AlertStateMachine stateMachine,
        RateLimitService rateLimitService,
        ILogger<MqttHandlerService> logger,
        IConfiguration configuration)
    {
        _stateMachine = stateMachine;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MQTT Handler Service starting...");

        try
        {
            await ConnectToMqttBroker(stoppingToken);

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MQTT Handler Service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MQTT Handler Service");
            throw;
        }
    }

    private async Task ConnectToMqttBroker(CancellationToken stoppingToken)
    {
        var brokerHost = _configuration["Mqtt:BrokerHost"] ?? "emqx";
        var brokerPort = int.Parse(_configuration["Mqtt:BrokerPort"] ?? "8883");
        var clientId = _configuration["Mqtt:ClientId"] ?? "policy-service";
        var certPath = _configuration["Mqtt:ClientCertPath"] ?? "/certs/policy-service/client.crt";
        var keyPath = _configuration["Mqtt:ClientKeyPath"] ?? "/certs/policy-service/client.key";
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
                    CertificateValidationHandler = context =>
                    {
                        if (context.Certificate == null)
                            return false;

                        // Validate against CA certificate
                        var chain = new X509Chain();
                        chain.ChainPolicy.ExtraStore.Add(caCert);
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                        var isValid = chain.Build(new X509Certificate2(context.Certificate));

                        if (!isValid)
                            return false;

                        // Verify the root CA matches our trusted CA
                        var chainRoot = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return chainRoot.Thumbprint.Equals(caCert.Thumbprint, StringComparison.OrdinalIgnoreCase);
                    },
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

        // Subscribe to alert topics
        var alertTopic = "tenant/+/building/+/room/+/alert";
        await _mqttClient!.SubscribeAsync(alertTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        _logger.LogInformation("Subscribed to alert topic: {Topic}", alertTopic);

        // Subscribe to PA status feedback
        var paStatusTopic = "pa/+/status";
        await _mqttClient.SubscribeAsync(paStatusTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
        _logger.LogInformation("Subscribed to PA status topic: {Topic}", paStatusTopic);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("Received MQTT message: Topic={Topic}, PayloadLength={Length}",
                topic, payload.Length);

            if (topic.StartsWith("tenant/") && topic.EndsWith("/alert"))
            {
                await HandleAlertTrigger(topic, payload);
            }
            else if (topic.StartsWith("pa/") && topic.EndsWith("/status"))
            {
                await HandlePaStatus(topic, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message: Topic={Topic}",
                args.ApplicationMessage.Topic);
            MqttMessagesTotal.WithLabels("unknown", "error").Inc();
        }
    }

    private async Task HandleAlertTrigger(string topic, string payload)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        try
        {
            // Parse alert trigger
            var trigger = JsonSerializer.Deserialize<AlertTrigger>(payload);
            if (trigger == null)
            {
                _logger.LogError("Failed to deserialize alert trigger: {Payload}", payload);
                MqttMessagesTotal.WithLabels("alert", "parse_error").Inc();
                return;
            }

            MqttMessagesTotal.WithLabels("alert", "received").Inc();

            _logger.LogInformation(
                "Alert trigger received: AlertId={AlertId}, Tenant={Tenant}, Building={Building}, " +
                "SourceRoom={Room}, Origin={Origin}, Device={DeviceId}",
                trigger.AlertId, trigger.TenantId, trigger.BuildingId, trigger.SourceRoomId, trigger.Origin, trigger.DeviceId);

            // Check rate limits (device + tenant)
            if (!_rateLimitService.CheckAlert(trigger.DeviceId, trigger.TenantId))
            {
                _logger.LogWarning(
                    "╔═══════════════════════════════════════════════════════════╗\n" +
                    "║   ⚠️  RATE LIMIT EXCEEDED - ALERT BLOCKED                ║\n" +
                    "╚═══════════════════════════════════════════════════════════╝\n" +
                    "Alert: {AlertId}, Device: {DeviceId}, Tenant: {TenantId}",
                    trigger.AlertId, trigger.DeviceId, trigger.TenantId);

                MqttMessagesTotal.WithLabels("alert", "rate_limited").Inc();
                return;
            }

            // Process through FSM
            var alertEvent = await _stateMachine.ProcessTrigger(trigger, receivedAt);
            if (alertEvent == null)
            {
                // Alert was rejected by FSM
                MqttMessagesTotal.WithLabels("alert", "rejected").Inc();
                return;
            }

            // Fan out PA commands to target rooms
            await FanOutPaCommands(alertEvent);

            MqttMessagesTotal.WithLabels("alert", "processed").Inc();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling alert trigger: Topic={Topic}", topic);
            MqttMessagesTotal.WithLabels("alert", "error").Inc();
        }
    }

    private async Task FanOutPaCommands(AlertEvent alertEvent)
    {
        _logger.LogInformation(
            "Fanning out PA commands: AlertId={AlertId}, TargetRooms={Count}",
            alertEvent.AlertId, alertEvent.TargetRooms.Count);

        foreach (var roomId in alertEvent.TargetRooms)
        {
            var paCommand = new PaCommand
            {
                AlertId = alertEvent.AlertId,
                RoomId = roomId,
                ClipRef = "EMERGENCY_ALERT", // TODO: Dynamic clip selection based on mode
                Mode = alertEvent.Mode,
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                CausalChainId = alertEvent.CausalChainId
            };

            var topic = $"pa/{roomId}/play";
            var payload = JsonSerializer.Serialize(paCommand);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1 for reliability
                .WithRetainFlag(false)
                .Build();

            await _mqttClient!.EnqueueAsync(message);

            _paCommandsSent++;
            MqttMessagesTotal.WithLabels("pa_command", "sent").Inc(); // Increment by 1, not cumulative

            _logger.LogInformation(
                "PA command sent: AlertId={AlertId}, Room={Room}, Topic={Topic}",
                alertEvent.AlertId, roomId, topic);
        }
    }

    private Task HandlePaStatus(string topic, string payload)
    {
        try
        {
            var paStatus = JsonSerializer.Deserialize<PaStatus>(payload);
            if (paStatus == null)
            {
                _logger.LogError("Failed to deserialize PA status: {Payload}", payload);
                return Task.CompletedTask;
            }

            _logger.LogInformation(
                "PA status received: AlertId={AlertId}, Room={Room}, Status={Status}",
                paStatus.AlertId, paStatus.RoomId, paStatus.Status);

            if (paStatus.Status == "OK" || paStatus.Status == "COMPLETED")
            {
                _paAcksReceived++;
            }

            // Update success ratio metric
            if (_paCommandsSent > 0)
            {
                var ratio = (double)_paAcksReceived / _paCommandsSent;
                PaPlaybackSuccessRatio.Set(ratio);
            }

            MqttMessagesTotal.WithLabels("pa_status", "received").Inc();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PA status: Topic={Topic}", topic);
            return Task.CompletedTask;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MQTT Handler Service...");

        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync();
            _mqttClient.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
