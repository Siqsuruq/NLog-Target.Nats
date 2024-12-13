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

using NATS;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using System.Text;
using System.Text.Json;

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
        public Layout Headers { get; set; } = new SimpleLayout("{}");

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
                _natsConnection.ConnectAsync();
                InternalLogger.Info("NATS connection successfully initialized.");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to initialize NATS connection.");
            }
        }

        protected override void Write(LogEventInfo logEvent)
        {
            InternalLogger.Info("Entering Write method.");

            var subject = this.RenderLogEvent(Subject, logEvent);
            var headersJson = this.RenderLogEvent(Headers, logEvent);
            var logMessage = this.RenderLogEvent(Layout, logEvent);

            // Check if the logEvent has an object to send
            var message = logEvent.Parameters?.Length > 0 ? logEvent.Parameters[0] : logEvent.Message;
            try
            {
                SendMessageAsync(message, subject, headersJson).Wait();
                InternalLogger.Info("SendMessageAsync completed successfully.");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to send message.");
            }
        }

        private async Task SendMessageAsync<T>(T message, string subject, string headersJson)
        {
            InternalLogger.Info("Publishing message via NatsJsonSerializer<T>.");
            try
            {
                NatsHeaders headers = ParseHeaders(headersJson);
                var serializer = new NatsJsonSerializer<T>();
                await _natsConnection.PublishAsync(subject: subject, headers: headers, data: message, serializer: serializer).ConfigureAwait(false);
                InternalLogger.Info("Message published successfully.");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Failed to publish message.");
            }
        }
        private NatsHeaders ParseHeaders(string headersJson)
        {
            var headers = new NatsHeaders();

            if (string.IsNullOrWhiteSpace(headersJson))
            {
                InternalLogger.Warn("Headers JSON is empty or whitespace. Using empty headers.");
                return headers;
            }

            try
            {
                // Parse JSON into a dictionary
                var parsedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Handle case-insensitive keys if necessary
                });

                if (parsedHeaders != null)
                {
                    foreach (var kvp in parsedHeaders)
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                        {
                            InternalLogger.Warn("Ignoring header with empty key or value: {0}:{1}", kvp.Key, kvp.Value);
                            continue;
                        }
                        headers[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    InternalLogger.Warn("Parsed headers are null. Using empty headers.");
                }
            }
            catch (JsonException ex)
            {
                InternalLogger.Error(ex, "Failed to parse headers JSON. Ensure valid JSON format. Headers JSON: {0}", headersJson);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Unexpected error occurred while parsing headers JSON: {0}", headersJson);
            }

            return headers;
        }

        protected override void CloseTarget()
        {
            _natsConnection?.DisposeAsync().AsTask().Wait();
            base.CloseTarget();
            InternalLogger.Info("NATS connection closed.");
        }
    }
}
