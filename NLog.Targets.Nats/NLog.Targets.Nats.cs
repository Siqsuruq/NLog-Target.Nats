// Copyright 2024 Maksym Zinchenko
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NATS.Client.Core;
using NATS.Client.Serializers.Json;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.Nats
{
    [Target("Nats")]
    public class NatsTarget : TargetWithLayout
    {
        private NatsConnection? _natsConnection;
        private NatsHeaders? _natsHeaders;
        private Task _lastWrite = Task.CompletedTask;

        [RequiredParameter]
        public Layout NatsUrl { get; set; } = new SimpleLayout(string.Empty);

        [RequiredParameter]
        public Layout Subject { get; set; } = new SimpleLayout(string.Empty);

        [ArrayParameter(typeof(TargetPropertyWithContext), "header")]
        public IList<TargetPropertyWithContext> Headers { get; } = new List<TargetPropertyWithContext>();

        public bool SingleParameter { get; set; }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var natsUrl = this.NatsUrl?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
            var subject = this.Subject?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;

            InternalLogger.Info($"Initializing NATS target with URL: {natsUrl} and Subject: {subject}");
            try
            {
                var options = new NatsOpts { Url = natsUrl };
                _natsConnection = new NatsConnection(options);
                ConnectAsyncToNats();
                InternalLogger.Info("NATS connection successfully initialized.");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to initialize NATS connection.");
            }

            NatsHeaders? headers = new NatsHeaders();
            foreach (var header in Headers)
            {
                var headerValue = header.Layout?.Render(LogEventInfo.CreateNullEvent());
                if (string.IsNullOrWhiteSpace(header.Name) || string.IsNullOrWhiteSpace(headerValue))
                {
                    InternalLogger.Warn("Ignoring NATS header with empty key or value: {0}={1}", header.Name, headerValue);
                    continue;
                }

                headers[header.Name] = headerValue;
            }

            _natsHeaders = headers.Count > 0 ? headers : null;
        }

        private async Task ConnectAsyncToNats()
        {
            try
            {
                await (_natsConnection?.ConnectAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to connect to NATS.");
            }
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var subject = this.RenderLogEvent(Subject, logEvent);
            var logMessage = this.RenderLogEvent(Layout, logEvent);

            // Check if the logEvent has an object to send
            if (SingleParameter && logEvent.Parameters?.Length == 1 && logEvent.Message?.StartsWith('{') == true && logEvent.Message?.EndsWith('}') == true)
            {
                _lastWrite = SendMessageAsync(logEvent.Parameters[0], subject);
            }
            else
            {
                _lastWrite = SendMessageAsync(logMessage, subject);
            }
        }

        private async Task SendMessageAsync<T>(T message, string subject)
        {
            try
            {
                var serializer = NatsJsonSerializerFactory<T>.Default;
                await (_natsConnection?.PublishAsync(subject: subject, headers: _natsHeaders, data: message, serializer: serializer) ?? ValueTask.CompletedTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to publish message to NATS.");
            }
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            _lastWrite.ContinueWith(t => asyncContinuation(t.Exception));
        }

        protected override void CloseTarget()
        {
            _natsConnection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            base.CloseTarget();
            InternalLogger.Info("NATS connection closed.");
        }

        private static class NatsJsonSerializerFactory<T>
        {
            static public readonly NatsJsonSerializer<T> Default = new NatsJsonSerializer<T>();
        }
    }
}
