# QUEDICHO

MVP de transcripción local en tiempo real para Windows 11. Captura la mezcla del dispositivo de salida mediante WASAPI Loopback, normaliza el audio a mono/16 kHz, descarta silencio, transcribe español con Whisper y persiste cada segmento en SQLite.

## Requisitos

- Windows 11 x64.
- SDK de .NET 9.0.301 o una revisión compatible de la banda 9.0.3xx.
- Microsoft Visual C++ Redistributable 2022 x64 para el runtime nativo de Whisper.
- Conexión a Internet solamente en el primer inicio de transcripción, para descargar `ggml-base.bin` (aproximadamente 142 MiB).

## Ejecutar

```powershell
dotnet restore SoftHolding.QUEDICHO.slnx
dotnet build SoftHolding.QUEDICHO.slnx
dotnet run --project src/SoftHolding.QUEDICHO.Desktop/SoftHolding.QUEDICHO.Desktop.csproj
```

En la ventana:

1. Selecciona el dispositivo que está reproduciendo el audio.
2. Pulsa **INICIAR**.
3. La primera ejecución descarga y carga el modelo local.
4. Reproduce una llamada, video o grabación con voz en español.
5. Usa **PAUSAR/REANUDAR** o **DETENER**.

## Datos locales

- Modelo: `%LOCALAPPDATA%\SoftHolding\QUEDICHO\Models`
- SQLite: `%LOCALAPPDATA%\SoftHolding\QUEDICHO\Data\quedicho.db`
- Logs: `%LOCALAPPDATA%\SoftHolding\QUEDICHO\Logs`

El MVP no almacena el audio capturado y no lo envía a servicios externos.

## Verificación

```powershell
dotnet test SoftHolding.QUEDICHO.slnx
```

La prueba física de audio debe repetirse con:

- altavoces integrados;
- audífonos cableados o USB;
- audífonos Bluetooth en perfil estéreo;
- cambio del dispositivo predeterminado durante una sesión.

WASAPI captura únicamente el endpoint seleccionado. Si una aplicación reproduce sonido en otro endpoint, debe elegirse ese dispositivo en QUEDICHO.
