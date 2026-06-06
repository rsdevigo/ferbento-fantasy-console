using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Ferbento.Core.Chips;
using Ferbento.Editor.ViewModels;
using Ferbento.Editor.Views;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Editor;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly Dictionary<string, Control> _views = new();
    private FerbentoRuntime? _runtime;
    private DispatcherTimer? _runTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        BuildViews();
        BuildTabBar();
        UpdateTabVisibility();
    }

    private void BuildViews()
    {
        _views["code"] = CodeEditor;
        var newViews = new (string id, Control view)[]
        {
            ("sprites", new SpriteEditorView()),
            ("tilemap", new TilemapEditorView()),
            ("palette", new PaletteEditorView()),
            ("sfx", new SfxEditorView()),
            ("music", new MusicTrackerView()),
            ("assets", new AssetBrowserView()),
            ("profiles", new ProfileEditorView()),
            ("debug", new DebugPanelView()),
            ("project", new ProjectManagerView()),
        };
        foreach (var (id, view) in newViews)
        {
            view.IsVisible = false;
            view.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            view.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            Grid.SetRow(view, 0);
            Grid.SetRowSpan(view, 2);
            ContentHost.Children.Add(view);
            _views[id] = view;
        }
    }

    private void BuildTabBar()
    {
        TabBar.Children.Clear();
        foreach (var tab in _viewModel.Tabs)
        {
            var button = new Button
            {
                Tag = tab.Id,
                Padding = new Thickness(12, 8),
                Background = Brushes.Transparent,
                Foreground = Brushes.LightGray,
                Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = tab.Icon },
                        new TextBlock { Text = tab.Title }
                    }
                }
            };
            button.Click += OnTabClick;
            TabBar.Children.Add(button);
        }
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tabId })
        {
            _viewModel.SelectedTabId = tabId;
            UpdateTabVisibility();
        }
    }

    private void UpdateTabVisibility()
    {
        var selected = _viewModel.SelectedTabId;
        foreach (var kv in _views)
            kv.Value.IsVisible = kv.Key == selected;
        PreviewPanel.IsVisible = selected == "code";
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        StopRunTimer();

        try
        {
            _runtime = new FerbentoRuntime(source: CodeEditor.SourceCode);
            _runtime.Sandbox.Context.PrintHandler = AppendConsole;
            _runtime.Loop.Boot();
            _runtime.Loop.Step();
            UpdateScreenPreview(_runtime.Bus);

            AppendConsole($"Running cart... Frame {_runtime.Loop.FrameCount}");

            _runTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) };
            _runTimer.Tick += (_, _) =>
            {
                if (_runtime == null) return;
                try
                {
                    _runtime.Loop.Step();
                    UpdateScreenPreview(_runtime.Bus);
                }
                catch (Exception ex)
                {
                    AppendConsole($"Runtime error: {ex.Message}");
                    StopRunTimer();
                }
            };
            _runTimer.Start();
        }
        catch (Exception ex)
        {
            AppendConsole(FormatError(ex));
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        StopRunTimer();
        AppendConsole("Stopped.");
    }

    private void StopRunTimer()
    {
        if (_runTimer == null) return;
        _runTimer.Stop();
        _runTimer = null;
    }

    private WriteableBitmap? _previewBitmap;

    private void UpdateScreenPreview(ChipBus bus)
    {
        var buffer = bus.Display.FrontBuffer;
        var palette = ColorChip.Pico8Palette;
        var width = buffer.Width;
        var height = buffer.Height;

        _previewBitmap ??= new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);

        using (var locked = _previewBitmap.Lock())
        {
            var pixels = new byte[locked.RowBytes * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var colorIndex = buffer.GetPixel(x, y) & 0xF;
                    var rgb = palette[colorIndex];
                    var offset = y * locked.RowBytes + x * 4;
                    pixels[offset] = (byte)((rgb >> 16) & 0xFF);
                    pixels[offset + 1] = (byte)((rgb >> 8) & 0xFF);
                    pixels[offset + 2] = (byte)(rgb & 0xFF);
                    pixels[offset + 3] = 255;
                }
            }

            Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
        }

        ScreenPreview.Source = _previewBitmap;
        ScreenPreview.InvalidateVisual();
    }

    private void AppendConsole(string message)
    {
        var current = ConsoleOutput.Text ?? "";
        ConsoleOutput.Text = string.IsNullOrEmpty(current) || current == "Ready."
            ? message
            : $"{current}\n{message}";
    }

    private static string FormatError(Exception ex) =>
        ex.InnerException == null
            ? $"Error: {ex.Message}"
            : $"Error: {ex.Message}\n  {ex.InnerException.Message}";
}
