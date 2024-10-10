// Morgan Stanley makes this available to you under the Apache License,
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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Ribbon;
using CommunityToolkit.Mvvm.ComponentModel;
using Infragistics.Windows.DockManager;
using MorganStanley.ComposeUI.LayoutPersistence.Abstractions;
using MorganStanley.ComposeUI.ModuleLoader;
using MorganStanley.ComposeUI.Shell.ImageSource;
using MorganStanley.ComposeUI.Shell.Utilities;

namespace MorganStanley.ComposeUI.Shell;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : RibbonWindow
{
    private readonly IModuleLoader _moduleLoader;
    private readonly IModuleCatalog _moduleCatalog;
    private readonly ImageSourceProvider _iconProvider;
    private readonly ILayoutPersistence<string> _layoutPersistence;

    public MainWindow(
        IModuleCatalog moduleCatalog,
        IModuleLoader moduleLoader,
        ILayoutPersistence<string> layoutPersistence,
        IImageSourcePolicy? imageSourcePolicy = null)
    {
        _moduleCatalog = moduleCatalog;
        _moduleLoader = moduleLoader;
        _layoutPersistence = layoutPersistence;
        _iconProvider = new ImageSourceProvider(imageSourcePolicy ?? new DefaultImageSourcePolicy());

        InitializeComponent();
    }

    private async void RibbonWindow_Initialized(object sender, System.EventArgs e)
    {
        var moduleIds = await _moduleCatalog.GetModuleIds();

        var modules = new List<ModuleViewModel>();
        foreach (var moduleId in moduleIds)
        {
            var manifest = await _moduleCatalog.GetManifest(moduleId);
            modules.Add(new ModuleViewModel(manifest, _iconProvider));
        }

        ViewModel = new MainWindowViewModel
        {
            Modules = new ObservableCollection<ModuleViewModel>(modules)
        };
    }

    public void AddDockableFloatingContent(WebContent webContent)
    {
        _verticalSplit.Panes.Add(new WebContentPane(webContent, _moduleLoader));
    }

    internal MainWindowViewModel ViewModel
    {
        get => (MainWindowViewModel) DataContext;
        private set => DataContext = value;
    }

    private async void StartModule_Click(object sender, RoutedEventArgs e)
    {
        // I ❤️ C#
        if (sender is FrameworkElement
            {
                DataContext: ModuleViewModel module
            })
        {
            await _moduleLoader.StartModule(new StartRequest(module.Manifest.Id));
        }
    }

    internal sealed class MainWindowViewModel : ObservableObject
    {
        public ObservableCollection<ModuleViewModel> Modules
        {
            get => _modules;
            set => SetProperty(ref _modules, value);
        }

        private ObservableCollection<ModuleViewModel> _modules = new();
    }

    internal sealed class ModuleViewModel
    {
        public ModuleViewModel(IModuleManifest manifest, ImageSourceProvider imageSourceProvider)
        {
            Manifest = manifest;

            if (manifest.TryGetDetails<WebManifestDetails>(out var webManifestDetails))
            {
                if (webManifestDetails.IconUrl != null)
                {
                    ImageSource = imageSourceProvider.GetImageSource(
                        webManifestDetails.IconUrl,
                        webManifestDetails.Url);
                }
            }
            else if (manifest.TryGetDetails<NativeManifestDetails>(out var nativeManifestDetails))
            {
                var path = nativeManifestDetails.Path.IsAbsoluteUri ? nativeManifestDetails.Path.AbsolutePath : nativeManifestDetails.Path.ToString();
                if (File.Exists(path))
                {
                    using var icon =
                        System.Drawing.Icon.ExtractAssociatedIcon(path);

                    if (icon != null)
                    {
                        using var bitmap = icon.ToBitmap();

                        ImageSource = bitmap.ToImageSource();
                    }
                }
            }
        }

        public IModuleManifest Manifest { get; }

        public System.Windows.Media.ImageSource? ImageSource { get; }
    }

    private Dictionary<string, WebWindowOptions> _windowContent;
    private Dictionary<string, List<string>> _injectedScripts;

    private async void LoadLayout_Click(object sender, RoutedEventArgs e)
    {
        var layout = await _layoutPersistence.LoadLayoutAsync("layout");
        var content = await _layoutPersistence.LoadLayoutAsync("content");
        var scripts = await _layoutPersistence.LoadLayoutAsync("scripts");
        _windowContent = JsonSerializer.Deserialize<Dictionary<string, WebWindowOptions>>(content);
        _injectedScripts = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scripts);
        _xamDockManager.LoadLayout(layout);
    }

    private async void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        var layout = _xamDockManager.SaveLayout();
        await _layoutPersistence.SaveLayoutAsync("layout", layout);
        await SaveContentAsync();
    }

    private async Task SaveContentAsync()
    {
        Dictionary<string, WebWindowOptions> panes = new Dictionary<string, WebWindowOptions>();
        Dictionary<string, List<string>> injectedScripts = new Dictionary<string, List<string>>();

        foreach (var pane in _xamDockManager.GetPanes(PaneNavigationOrder.VisibleOrder))
        {
            if (pane is WebContentPane)
            {
                var options = ((WebContentPane)pane).WebContent.Options;
                var scripts = ((WebContentPane) pane).WebContent.InjectedScripts;
                options.Title = (string)pane.Header;
                panes.Add(pane.SerializationId, options);
                injectedScripts.Add(pane.SerializationId, scripts);
            }
        }

        await _layoutPersistence.SaveLayoutAsync("content", JsonSerializer.Serialize(panes));
        await _layoutPersistence.SaveLayoutAsync("scripts", JsonSerializer.Serialize(injectedScripts));
    }

    private void XamDockManager_InitializePaneContent(object sender, Infragistics.Windows.DockManager.Events.InitializePaneContentEventArgs e)
    {
        var id = e.NewPane.SerializationId;
        var webContent = new WebContent(_windowContent[id], _moduleLoader)
        {
            InjectedScripts = _injectedScripts[id]
        };

        e.NewPane = new WebContentPane(webContent, _moduleLoader);

    }
}