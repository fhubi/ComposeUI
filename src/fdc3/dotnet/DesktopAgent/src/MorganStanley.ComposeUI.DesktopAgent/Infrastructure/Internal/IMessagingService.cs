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

using System.Text.Json;

namespace MorganStanley.ComposeUI.Fdc3.MorganStanley.ComposeUI.DesktopAgent.Infrastructure.Internal
{
    public interface IMessageBuffer
    {
        ReadOnlySpan<byte> GetSpan();
        string GetString();
        T? ReadJson<T>(JsonSerializerOptions? options = null);
    }

    public interface IMessageContext { }

    public interface IMessagingService
    {
        ValueTask ConnectAsync(CancellationToken cancellationToken = default);
        ValueTask<IAsyncDisposable> SubscribeAsync(string topic, Func<IMessageBuffer, ValueTask> handler, CancellationToken cancellationToken = default);
        ValueTask RegisterServiceAsync(string endpoint, Func<string, IMessageBuffer?, IMessageContext?, ValueTask<IMessageBuffer?>> handler, CancellationToken cancellationToken = default);
        ValueTask UnregisterServiceAsync(string endpoint, CancellationToken cancellationToken = default);
        ValueTask PublishAsync(string topic, IMessageBuffer message, CancellationToken cancellationToken = default);
        string? ClientId { get; }
    }
}
