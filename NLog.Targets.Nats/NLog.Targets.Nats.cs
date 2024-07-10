using NATS;
using NATS.Client.Core;
using NLog;
using NLog.Common;
using NLog.Config;
using System.Text;

namespace NLog.Targets.Nats
{
    [Target("Nats")]
    public class NatsTarget : TargetWithLayout
    {
        private NatsConnection _natsConnection;

        [RequiredParameter]
        public string NatsUrl { get; set; }

        [RequiredParameter]
        public string Subject { get; set; }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            InternalLogger.Info($"Initializing NATS target with URL: {NatsUrl} and Subject: {Subject}");
            var options = new NatsOpts { Url = NatsUrl };
            _natsConnection = new NatsConnection(options);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var logMessage = this.Layout.Render(logEvent);
            InternalLogger.Info($"Publishing log message: {logMessage}");
            SendMessageAsync(logMessage);
        }

        private async Task SendMessageAsync(string message)
        {
            try
            {
                var messageData = Encoding.UTF8.GetBytes(message);
                await _natsConnection.PublishAsync(Subject, messageData);
                InternalLogger.Info($"Successfully published message: {message}");
            }
            catch (Exception ex)
            {
                InternalLogger.Error($"Failed to publish message: {ex.Message}");
            }
        }

        protected override void CloseTarget()
        {
            _natsConnection?.DisposeAsync().AsTask().Wait();
            base.CloseTarget();
            InternalLogger.Info("NATS connection closed.");
        }
    }
}
