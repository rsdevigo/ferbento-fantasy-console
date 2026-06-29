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
using Ferbento.Profiles.BuiltIn;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Editor;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly EditorSession _session = new(BuiltInProfiles.Pico8());
    private readonly Dictionary<string, Control> _views = new();
    private FerbentoRuntime? _runtime;
    private DispatcherTimer? _runTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        WireSession();
        BuildViews();
        BuildTabBar();
        UpdateTabVisibility();
        UpdateWindowTitle();
    }

    private void WireSession()
    {
        _session.GetSourceCode = () => CodeEditor.SourceCode;
        _session.SetSourceCode = code => CodeEditor.SourceCode = code;
        _session.DirtyChanged += UpdateWindowTitle;
        CodeEditor.SourceCodeChanged += (_, _) => _session.MarkDirty();
    }

    private void UpdateWindowTitle()
    {
        var dirty = _session.IsDirty ? " *" : "";
        Title = $"{_session.ProjectDisplayName}{dirty} — Ferbento";
    }

    private void BuildViews()
    {
        _views["code"] = CodeEditor;

        SpriteEditorView? spriteView = null;
        TilemapEditorView? tilemapView = null;
        PaletteEditorView? paletteView = null;
        ProfileEditorView? profileView = null;
        MusicTrackerView? musicView = null;

        SfxEditorView? sfxView = null;

        var newViews = new (string id, Control view)[]
        {
            ("sprites", spriteView = new SpriteEditorView()),
            ("tilemap", tilemapView = new TilemapEditorView()),
            ("palette", paletteView = new PaletteEditorView()),
            ("sfx", sfxView = new SfxEditorView()),
            ("music", musicView = new MusicTrackerView()),
            ("assets", new AssetBrowserView()),
            ("profiles", profileView = new ProfileEditorView()),
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

            if (view is IProfileAwareEditor editor)
                _session.RegisterEditor(editor);
        }

        profileView?.SetProfileChangeHandler(async profile =>
        {
            var applied = await _session.RequestProfileChangeAsync(profile, this);
            if (applied)
                profileView.ShowProfile(_session.Profile);
            return applied;
        });
        profileView?.ShowProfile(_session.Profile);

        var markDirty = () => _session.MarkDirty();
        spriteView!.SetEditedHandler(markDirty);
        tilemapView!.SetEditedHandler(markDirty);
        paletteView!.SetEditedHandler(markDirty);
        musicView!.SetEditedHandler(markDirty);
        sfxView!.SetEditedHandler(markDirty);

        _session.GetMusicProject = () => musicView.ExportMusicData();
        _session.SetMusicProject = data => musicView.ImportMusicData(data);
        _session.GetSfxProject = () => sfxView.ExportSfxProjectData();
        _session.SetSfxProject = data => sfxView.ImportSfxProjectData(data);
        _session.GetSfxSlotColor = slot => sfxView.Controller.GetSlotColor(slot);
        musicView.SetProjectLabel($"{_session.ProjectDisplayName}/MUSIC.JSON");
        _session.DirtyChanged += () => musicView.SetProjectLabel($"{_session.ProjectDisplayName}/MUSIC.JSON");

        paletteView!.PaletteChanged += () =>
        {
            spriteView?.RefreshPalette();
            tilemapView?.RefreshPalette();
            sfxView?.RefreshPalette();
        };

        tilemapView!.SetSpriteController(spriteView!.Controller);
        spriteView!.Controller.Edited += () => tilemapView.RefreshFromBus();
        tilemapView!.Controller.Edited += () => spriteView.RefreshFromBus();

        _session.PropagateToEditors();
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
            _runtime = new FerbentoRuntime(
                bus: _session.Bus,
                source: CodeEditor.SourceCode,
                preserveCartData: true);
            _runtime.Sandbox.Context.PrintHandler = AppendConsole;
            _runtime.Loop.Boot();
            _runtime.Loop.Step();
            UpdateScreenPreview(_session.Bus);

            AppendConsole($"Running cart... Frame {_runtime.Loop.FrameCount}");

            _runTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) };
            _runTimer.Tick += (_, _) =>
            {
                if (_runtime == null) return;
                try
                {
                    _runtime.Loop.Step();
                    UpdateScreenPreview(_session.Bus);
                }
                catch (Exception ex)
                {
                    AppendConsole($"Runtime error: {ex.Message}");
                    StopRunTimer();
                    _runtime = null;
                    _session.RefreshEditorsFromBus();
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
        _runtime = null;
        _session.RefreshEditorsFromBus();
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
        var colorChip = bus.Color;
        var width = buffer.Width;
        var height = buffer.Height;

        _previewBitmap ??= new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);

        if (_previewBitmap.PixelSize.Width != width || _previewBitmap.PixelSize.Height != height)
        {
            _previewBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Opaque);
        }

        using (var locked = _previewBitmap.Lock())
        {
            var pixels = new byte[locked.RowBytes * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var colorIndex = buffer.GetPixel(x, y) & 0xF;
                    var rgb = colorChip.GetColor(colorIndex);
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
