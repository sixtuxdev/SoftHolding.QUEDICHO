[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version = '1.0.0',

    [string] $ModelPath,

    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'SoftHolding.QUEDICHO.slnx'
$desktopProject = Join-Path $repositoryRoot 'src\SoftHolding.QUEDICHO.Desktop\SoftHolding.QUEDICHO.Desktop.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\SoftHolding.QUEDICHO.nsi'
$installerRoot = Join-Path $repositoryRoot 'artifacts\installer'
$publishRoot = Join-Path $installerRoot 'publish'
$cacheDirectory = Join-Path $installerRoot 'cache'
$toolsDirectory = Join-Path $installerRoot 'tools'
$outputDirectory = Join-Path $installerRoot 'output'
$cachedModelPath = Join-Path $cacheDirectory 'ggml-base.bin'
$vcRedistPath = Join-Path $cacheDirectory 'VC_redist.x64.exe'
$nsisArchivePath = Join-Path $cacheDirectory 'nsis-3.12.zip'
$nsisToolDirectory = Join-Path $toolsDirectory 'nsis-3.12'
$expectedModelHash = '60ED5BC3DD14EEA856493D334349B405782DDCAF0028D4B5DF4088345FBA2EFE'
$expectedNsisArchiveHash = '56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F'
$modelDownloadUri = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin'
$vcRedistDownloadUri = 'https://aka.ms/vc14/vc_redist.x64.exe'
$nsisDownloadUri = 'https://prdownloads.sourceforge.net/nsis/nsis-3.12.zip?download'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string] $Command,

        [Parameter(ValueFromRemainingArguments)]
        [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "El comando '$Command' terminó con el código $LASTEXITCODE."
    }
}

function Get-NsisCompiler {
    $command = Get-Command 'makensis.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidatePaths = @(
        (Join-Path $env:ProgramFiles 'NSIS\makensis.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe')
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    $downloadNsis = -not (Test-Path -LiteralPath $nsisArchivePath -PathType Leaf)
    if (-not $downloadNsis) {
        $downloadNsis = (Get-FileHash -LiteralPath $nsisArchivePath -Algorithm SHA256).Hash -ne
            $expectedNsisArchiveHash
    }

    if ($downloadNsis) {
        Write-Host 'Descargando el compilador portátil NSIS 3.12...'
        $temporaryArchivePath = $nsisArchivePath + '.download'
        try {
            & curl.exe '--fail' '--location' '--silent' '--show-error' `
                '--output' $temporaryArchivePath $nsisDownloadUri
            if ($LASTEXITCODE -ne 0) {
                throw "No se pudo descargar NSIS. Código de curl: $LASTEXITCODE"
            }

            Move-Item -LiteralPath $temporaryArchivePath -Destination $nsisArchivePath -Force
        }
        finally {
            if (Test-Path -LiteralPath $temporaryArchivePath -PathType Leaf) {
                Remove-Item -LiteralPath $temporaryArchivePath -Force
            }
        }
    }

    $archiveHash = (Get-FileHash -LiteralPath $nsisArchivePath -Algorithm SHA256).Hash
    if ($archiveHash -ne $expectedNsisArchiveHash) {
        throw 'El paquete de NSIS no superó la comprobación SHA-256.'
    }

    if (-not (Test-Path -LiteralPath $nsisToolDirectory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $nsisToolDirectory | Out-Null
        Expand-Archive -LiteralPath $nsisArchivePath -DestinationPath $nsisToolDirectory -Force
    }

    $portableCompiler = Get-ChildItem -LiteralPath $nsisToolDirectory -Filter 'makensis.exe' -File -Recurse |
        Select-Object -First 1
    if ($null -eq $portableCompiler) {
        throw 'No se encontró makensis.exe dentro del paquete verificado de NSIS.'
    }

    return $portableCompiler.FullName
}

function Assert-ModelHash {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedModelHash) {
        throw "El modelo Whisper no superó la comprobación SHA-256. Archivo: $Path"
    }
}

function Reset-BuildDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullInstallerRoot = [IO.Path]::GetFullPath($installerRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $requiredPrefix = $fullInstallerRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Se rechazó limpiar una ruta fuera del directorio de artefactos: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
}

function Remove-UnusedNativeRuntimes {
    param(
        [Parameter(Mandatory)]
        [string] $PublishDirectory,

        [Parameter(Mandatory)]
        [string] $RuntimeIdentifier
    )

    $runtimeDirectory = Join-Path $PublishDirectory 'runtimes'
    if (-not (Test-Path -LiteralPath $runtimeDirectory -PathType Container)) {
        return
    }

    Get-ChildItem -LiteralPath $runtimeDirectory -Directory |
        Where-Object Name -ne $RuntimeIdentifier |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }
}

New-Item -ItemType Directory -Force -Path $publishRoot, $cacheDirectory, $toolsDirectory, $outputDirectory | Out-Null

if (-not $SkipTests) {
    Write-Host 'Ejecutando pruebas...'
    Invoke-CheckedCommand dotnet 'test' $solutionPath '--configuration' 'Release'
}

foreach ($runtimeIdentifier in @('win-x64', 'win-arm64')) {
    $publishDirectory = Join-Path $publishRoot $runtimeIdentifier
    Reset-BuildDirectory -Path $publishDirectory
    Write-Host "Publicando $runtimeIdentifier..."
    Invoke-CheckedCommand dotnet `
        'publish' $desktopProject `
        '--configuration' 'Release' `
        '--runtime' $runtimeIdentifier `
        '--self-contained' 'true' `
        '--output' $publishDirectory `
        "-p:Version=$Version" `
        '-p:DebugType=None' `
        '-p:DebugSymbols=false' `
        '-p:PublishSingleFile=false'
    Remove-UnusedNativeRuntimes `
        -PublishDirectory $publishDirectory `
        -RuntimeIdentifier $runtimeIdentifier
}

if ([string]::IsNullOrWhiteSpace($ModelPath)) {
    $installedModelPath = Join-Path $env:LOCALAPPDATA 'SoftHolding\QUEDICHO\Models\ggml-base.bin'
    if (Test-Path -LiteralPath $installedModelPath -PathType Leaf) {
        $ModelPath = $installedModelPath
    }
    elseif (Test-Path -LiteralPath $cachedModelPath -PathType Leaf) {
        $ModelPath = $cachedModelPath
    }
    else {
        Write-Host 'Descargando el modelo Whisper base...'
        Invoke-WebRequest -Uri $modelDownloadUri -OutFile $cachedModelPath
        $ModelPath = $cachedModelPath
    }
}

$ModelPath = (Resolve-Path -LiteralPath $ModelPath).Path
Assert-ModelHash -Path $ModelPath

$downloadVcRedist = $true
if (Test-Path -LiteralPath $vcRedistPath -PathType Leaf) {
    $existingSignature = Get-AuthenticodeSignature -LiteralPath $vcRedistPath
    $downloadVcRedist = $existingSignature.Status -ne 'Valid' -or
        $existingSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'
}

if ($downloadVcRedist) {
    Write-Host 'Descargando Microsoft Visual C++ Redistributable...'
    Invoke-WebRequest -Uri $vcRedistDownloadUri -OutFile $vcRedistPath
}

$vcRedistSignature = Get-AuthenticodeSignature -LiteralPath $vcRedistPath
if ($vcRedistSignature.Status -ne 'Valid' -or
    $vcRedistSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
    throw 'El redistribuible de Visual C++ no tiene una firma digital válida de Microsoft.'
}

$installerPath = Join-Path $outputDirectory "SoftHolding.QUEDICHO-Setup-$Version-Windows11.exe"
if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
    Remove-Item -LiteralPath $installerPath -Force
}

$nsisCompiler = Get-NsisCompiler
Write-Host 'Compilando el instalador universal para Windows 11...'
Invoke-CheckedCommand $nsisCompiler `
    "/DAPP_VERSION=$Version" `
    "/DPUBLISH_X64_DIR=$(Join-Path $publishRoot 'win-x64')" `
    "/DPUBLISH_ARM64_DIR=$(Join-Path $publishRoot 'win-arm64')" `
    "/DMODEL_PATH=$ModelPath" `
    "/DVC_REDIST_PATH=$vcRedistPath" `
    "/DOUTPUT_PATH=$installerPath" `
    $installerScript

if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "El compilador no generó el instalador esperado: $installerPath"
}

$installerFile = Get-Item -LiteralPath $installerPath
$installerHash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
$checksumPath = $installerPath + '.sha256'
$checksumLine = "$($installerHash.Hash) *$($installerFile.Name)$([Environment]::NewLine)"
[IO.File]::WriteAllText($checksumPath, $checksumLine, [Text.UTF8Encoding]::new($false))
Write-Host ''
Write-Host 'Instalador generado correctamente:'
Write-Host "  Archivo: $($installerFile.FullName)"
Write-Host "  Tamaño:  $([Math]::Round($installerFile.Length / 1MB, 2)) MiB"
Write-Host "  SHA-256: $($installerHash.Hash)"
Write-Host "  Suma:    $checksumPath"
