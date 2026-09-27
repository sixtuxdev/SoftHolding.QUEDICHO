using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed class TextInjectionPreferencesStore(
    IOptions<TextInjectionOptions> options,
    ILogger<TextInjectionPreferencesStore> logger) : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly TextInjectionOptions _defaults = options.Value;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoftHolding",
        "QUEDICHO",
        "Settings",
        "text-injection.json");

    public async Task<TextInjectionPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        var defaults = CreateDefaults();
        if (!File.Exists(_settingsPath))
        {
            return defaults;
        }

        try
        {
            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var preferences = await JsonSerializer.DeserializeAsync<TextInjectionPreferences>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (preferences is null || string.IsNullOrWhiteSpace(preferences.GlobalHotkey))
            {
                return defaults;
            }

            preferences.GlobalHotkey = preferences.GlobalHotkey.Trim();
            return preferences;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TextInjectionLog.PreferencesLoadFailed(logger, exception);
            return defaults;
        }
    }

    public async Task SaveAsync(TextInjectionPreferences preferences, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)
                            ?? throw new InvalidOperationException("The settings directory could not be resolved.");
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, preferences, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TextInjectionLog.PreferencesSaveFailed(logger, exception);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private TextInjectionPreferences CreateDefaults() => new()
    {
        Enabled = _defaults.Enabled,
        GlobalHotkey = string.IsNullOrWhiteSpace(_defaults.GlobalHotkey)
            ? "Ctrl+Shift+Y"
            : _defaults.GlobalHotkey.Trim()
    };

    public void Dispose() => _writeGate.Dispose();
}
