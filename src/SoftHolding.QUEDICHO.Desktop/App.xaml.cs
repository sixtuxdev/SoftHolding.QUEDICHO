using System.IO;
using System.Globalization;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SoftHolding.QUEDICHO.Application.Sessions;
using SoftHolding.QUEDICHO.Audio;
using SoftHolding.QUEDICHO.Desktop.ViewModels;
using SoftHolding.QUEDICHO.Desktop.TextInjection;
using SoftHolding.QUEDICHO.Infrastructure;
using SoftHolding.QUEDICHO.Infrastructure.Persistence;
using SoftHolding.QUEDICHO.Transcription;

namespace SoftHolding.QUEDICHO.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftHolding",
            "QUEDICHO",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Async(sink => sink.File(
                Path.Combine(logDirectory, "quedicho-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: CultureInfo.InvariantCulture))
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Services.AddSerilog();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddWindowsAudio(builder.Configuration);
            builder.Services.AddLocalWhisperTranscription(builder.Configuration);
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddWindowsTextInjection(builder.Configuration);
            builder.Services.AddSingleton<ITranscriptionSessionCoordinator, TranscriptionSessionCoordinator>();
            builder.Services.AddSingleton<MainWindowViewModel>();
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            await _host.StartAsync().ConfigureAwait(true);
            await _host.Services.GetRequiredService<DatabaseInitializer>()
                .InitializeAsync()
                .ConfigureAwait(true);

            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application startup failed");
            MessageBox.Show(
                $"QUEDICHO no pudo iniciar.\n\n{exception.Message}\n\nRevisa los logs en:\n{logDirectory}",
                "Error al iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                var coordinator = _host.Services.GetService<ITranscriptionSessionCoordinator>();
                if (coordinator is not null)
                {
                    coordinator.StopAsync().GetAwaiter().GetResult();
                }

                _host.StopAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                _host.Dispose();
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Application shutdown failed");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
