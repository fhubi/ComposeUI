/*
 * Morgan Stanley makes this available to you under the Apache License,
 * Version 2.0 (the "License"). You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0.
 *
 * See the NOTICE file distributed with this work for additional information
 * regarding copyright ownership. Unless required by applicable law or agreed
 * to in writing, software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
 * or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System.Reactive.Linq;
using MorganStanley.ComposeUI.Messaging;

namespace MorganStanley.ComposeUI.Fdc3.MorganStanley.ComposeUI.DesktopAgent.Infrastructure.Internal
{
    internal class MessageRouterAdapter : IMessagingService
    {
        private readonly IMessageRouter _messageRouter;

        public MessageRouterAdapter(IMessageRouter messageRouter)
        {
            _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        }

        public string? ClientId => _messageRouter.ClientId;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            return _messageRouter.ConnectAsync(cancellationToken);
        }

        public ValueTask PublishAsync(string topic, IMessageBuffer message, CancellationToken cancellationToken = default)
        {
            return _messageRouter.PublishAsync(topic, ((MessageBufferAdapter) message).MessageBuffer, cancellationToken: cancellationToken);
        }

        public ValueTask RegisterServiceAsync(string endpoint, Func<string, IMessageBuffer?, IMessageContext?, ValueTask<IMessageBuffer?>> handler, CancellationToken cancellationToken = default)
        {
            MessageHandler adaptedHandler = async (string endpoint, MessageBuffer? payload, MessageContext context) =>
            {
                var payloadAdapter = payload != null ? new MessageBufferAdapter(payload) : null;
                var contextAdapter = new MessageContextAdapter(context);
                var result = await handler(endpoint, payloadAdapter, contextAdapter);
                return result is MessageBufferAdapter adapter ? adapter.MessageBuffer : null;
            };

            return _messageRouter.RegisterServiceAsync(endpoint, adaptedHandler, cancellationToken: cancellationToken);
        }

        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic, Func<IMessageBuffer, ValueTask> handler, CancellationToken cancellationToken = default)
        {
            var observer = AsyncObserver.Create<TopicMessage>(
                async message =>
                {
                    var adapter = new MessageBufferAdapter(message.Payload);
                    await handler(adapter);
                });

            var subscription = await _messageRouter.SubscribeAsync(topic, observer, cancellationToken);
            return subscription;
        }

        public ValueTask UnregisterServiceAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return _messageRouter.UnregisterServiceAsync(endpoint, cancellationToken);
        }
    }
}
