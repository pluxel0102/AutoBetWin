using Microsoft.UI.Xaml;
using System;

namespace AutoBet;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Логирование ошибки
        System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception}");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "error.log"),
            $"{DateTime.Now}: {e.Exception}\n{e.Exception.StackTrace}"
        );
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "launch_error.log"),
                $"{DateTime.Now}: {ex}\n{ex.StackTrace}"
            );
            throw;
        }
    }

    private Window? m_window;
}
