﻿// Morgan Stanley makes this available to you under the Apache License,
// Version 2.0 (the "License"). You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0.
// 
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership. Unless required by applicable law or agreed
// to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
// or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MorganStanley.ComposeUI.ModuleLoader;

namespace MorganStanley.ComposeUI.Shell.Modules;

internal sealed class ModuleService : IHostedService
{
    private readonly IModuleLoader _moduleLoader;
    private ConcurrentBag<object> _disposables = new();
    private readonly ILogger<ModuleService> _logger;

    public ModuleService(IModuleLoader moduleLoader, ILogger<ModuleService>? logger = null)
    {
        _moduleLoader = moduleLoader;
        _logger = logger ?? NullLogger<ModuleService>.Instance;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _disposables.Add(
            _moduleLoader.LifetimeEvents
                .OfType<LifetimeEvent.Started>()
                .Where(e => e.Instance.Manifest.ModuleType == ModuleType.Web)
                .Subscribe(OnWebModuleStarted));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var disposable in _disposables)
        {
            (disposable as IDisposable)?.Dispose();
        }

        return Task.CompletedTask;
    }

    private async void OnWebModuleStarted(LifetimeEvent.Started e)
    {
        var properties = e.Instance.GetProperties().OfType<WebStartupProperties>().FirstOrDefault();
        if (properties == null)
        {
            return;
        }

        var webWindowOptions = e.Instance.GetProperties().OfType<WebWindowOptions>().FirstOrDefault();

        var parameters = new List<object>
        {
            e.Instance,
            webWindowOptions
                ?? new WebWindowOptions
                {
                    Url = properties.Url.ToString(),
                    IconUrl = properties.IconUrl?.ToString(),
                    InitialModulePostion = properties.InitialModulePosition,
                    Width = properties.Width ?? WebWindowOptions.DefaultWidth,
                    Height = properties.Height ?? WebWindowOptions.DefaultHeight,
                    Coordinates = properties.Coordinates
                }
        };

        try
        {
            await App.Current.Dispatcher.InvokeAsync(
                () =>
                {
                    var window = App.Current.CreateWebContent(parameters.ToArray());
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown when trying to create a web window: {ExceptionType}: {ExceptionMessage}", ex.GetType().FullName, ex.Message);
        }
    }
}