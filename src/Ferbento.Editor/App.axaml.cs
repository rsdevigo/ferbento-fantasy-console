using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ferbento.Editor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                LogCrash(e.Exception);
                e.Handled = false;
            };

            try
            {
                desktop.MainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                LogCrash(ex);
                desktop.MainWindow = CreateStartupErrorWindow(ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateStartupErrorWindow(Exception ex)
    {
        var hint = ex is FileLoadException or BadImageFormatException
            ? "\n\nO Windows Smart App Control pode estar bloqueando DLLs compiladas localmente. " +
              "Abra Segurança do Windows → Controle de aplicativos e navegador → " +
              "Configurações do Smart App Control e desative-o, ou ative o Modo de Desenvolvedor " +
              "em Configurações → Privacidade e segurança → Para desenvolvedores."
            : string.Empty;

        return new Window
        {
            Title = "Ferbento — erro ao iniciar",
            Width = 720,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new ScrollViewer
            {
                Padding = new Thickness(16),
                Content = new TextBlock
                {
                    Text = ex + hint,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 12,
                },
            },
        };
    }

    private static void LogCrash(Exception ex)
    {
        var text = ex.ToString();
        Console.Error.WriteLine(text);
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ferbento-crash.log");
            File.WriteAllText(path, text);
        }
        catch
        {
            // ignore secondary failures while reporting the original crash
        }
    }
}