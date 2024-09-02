using NATS;
using NATS.Client.Core;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using System.Text;

namespace NLog.Targets.Nats
{
    [Target("Nats")]
    public class NatsTarget : TargetWithLayout
    {
        private NatsConnection _natsConnection;

        [RequiredParameter]
        public Layout NatsUrl { get; set; } = new SimpleLayout(string.Empty);

        [RequiredParameter]
        public Layout Subject { get; set; } = new SimpleLayout(string.Empty);

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var natsUrl = this.NatsUrl?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
            var subject = this.Subject?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;

            InternalLogger.Info($"Initializing NATS target with URL: {natsUrl} and Subject: {subject}");
            var options = new NatsOpts { Url = natsUrl };
            _natsConnection = new NatsConnection(options);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var logMessage = this.RenderLogEvent(Layout, logEvent);
            var subject = this.RenderLogEvent(Subject, logEvent);
            SendMessageAsync(logMessage, subject);
        }

        private async Task SendMessageAsync(string message, string subject)
        {
            try
            {
                var messageData = Encoding.UTF8.GetBytes(message);
                await _natsConnection.PublishAsync(subject, messageData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to publish message: {0}", message);
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
