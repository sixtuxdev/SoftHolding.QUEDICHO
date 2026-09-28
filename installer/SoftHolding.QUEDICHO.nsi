Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef PUBLISH_X64_DIR
  !error "Falta PUBLISH_X64_DIR"
!endif
!ifndef PUBLISH_ARM64_DIR
  !error "Falta PUBLISH_ARM64_DIR"
!endif
!ifndef MODEL_PATH
  !error "Falta MODEL_PATH"
!endif
!ifndef VC_REDIST_PATH
  !error "Falta VC_REDIST_PATH"
!endif
!ifndef OUTPUT_PATH
  !define OUTPUT_PATH "SoftHolding.QUEDICHO-Setup-${APP_VERSION}-Windows11.exe"
!endif

!define APP_NAME "SoftHolding QUEDICHO"
!define APP_EXECUTABLE "SoftHolding.QUEDICHO.Desktop.exe"
!define APP_REGISTRY_KEY "Software\SoftHolding\QUEDICHO"
!define UNINSTALL_REGISTRY_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SoftHolding.QUEDICHO"

Name "${APP_NAME}"
OutFile "${OUTPUT_PATH}"
InstallDir "$PROGRAMFILES64\SoftHolding\QUEDICHO"
RequestExecutionLevel admin
SetCompressor /SOLID zlib
ManifestSupportedOS Win10
BrandingText "SoftHolding"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "SoftHolding"
VIAddVersionKey /LANG=1033 "FileDescription" "Instalador de ${APP_NAME}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (C) 2026 SoftHolding"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXECUTABLE}"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Spanish"
!insertmacro MUI_LANGUAGE "English"

Var NativeArchitecture

Function .onInit
  SetShellVarContext all

  GetWinVer $0 Build
  IntCmpU $0 22000 windows11 unsupported windows11

unsupported:
  MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requiere Windows 11 o una versión posterior."
  Quit

windows11:
  ReadEnvStr $0 "PROCESSOR_ARCHITEW6432"
  StrCmp $0 "ARM64" arm64
  ReadEnvStr $0 "PROCESSOR_ARCHITECTURE"
  StrCmp $0 "ARM64" arm64 x64

arm64:
  StrCpy $NativeArchitecture "arm64"
  Goto architectureReady

x64:
  StrCpy $NativeArchitecture "x64"

architectureReady:
FunctionEnd

Function EnsureApplicationIsClosed
retry:
  FindWindow $0 "" "QUEDICHO — Transcripción local"
  IntCmp $0 0 closed
  MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION \
    "Cierra QUEDICHO antes de continuar con la instalación." \
    IDRETRY retry IDCANCEL cancelled

cancelled:
  Abort

closed:
FunctionEnd

Function EnsureVcRuntime
  SetRegView 64
  StrCpy $0 "x64"
  StrCmp $NativeArchitecture "arm64" 0 checkRuntime
  StrCpy $0 "arm64"

checkRuntime:
  ClearErrors
  ReadRegDWORD $1 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\$0" "Installed"
  IfErrors installRuntime
  IntCmp $1 1 runtimeReady installRuntime installRuntime

installRuntime:
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=VC_redist.x64.exe "${VC_REDIST_PATH}"
  DetailPrint "Instalando Microsoft Visual C++ Redistributable..."
  ExecWait '"$PLUGINSDIR\VC_redist.x64.exe" /install /quiet /norestart' $1
  ${If} $1 != 0
  ${AndIf} $1 != 3010
  ${AndIf} $1 != 1638
    MessageBox MB_OK|MB_ICONSTOP \
      "No se pudo instalar Microsoft Visual C++ Redistributable. Código: $1"
    Abort
  ${EndIf}

runtimeReady:
FunctionEnd

Section "${APP_NAME} (requerido)" SecMain
  SectionIn RO
  Call EnsureApplicationIsClosed

  IfFileExists "$INSTDIR\.quedicho-install" 0 keepExistingDirectory
  RMDir /r "$INSTDIR"

keepExistingDirectory:
  SetOutPath "$INSTDIR"
  ${If} $NativeArchitecture == "arm64"
    File /r "${PUBLISH_ARM64_DIR}\*"
  ${Else}
    File /r "${PUBLISH_X64_DIR}\*"
  ${EndIf}

  SetOutPath "$INSTDIR\Models"
  File /oname=ggml-base.bin "${MODEL_PATH}"

  Call EnsureVcRuntime

  FileOpen $0 "$INSTDIR\.quedicho-install" w
  FileWrite $0 "${APP_VERSION}"
  FileClose $0

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\SoftHolding"
  CreateShortcut "$SMPROGRAMS\SoftHolding\${APP_NAME}.lnk" "$INSTDIR\${APP_EXECUTABLE}" "" "$INSTDIR\${APP_EXECUTABLE}"

  SetRegView 64
  WriteRegStr HKLM "${APP_REGISTRY_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_REGISTRY_KEY}" "Version" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "Publisher" "SoftHolding"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXECUTABLE}"
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_REGISTRY_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINSTALL_REGISTRY_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_REGISTRY_KEY}" "NoRepair" 1
SectionEnd

Section /o "Acceso directo en el escritorio" SecDesktopShortcut
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXECUTABLE}" "" "$INSTDIR\${APP_EXECUTABLE}"
SectionEnd

LangString DESC_SecMain ${LANG_SPANISH} "Instala QUEDICHO, el runtime de .NET y el modelo Whisper local."
LangString DESC_SecMain ${LANG_ENGLISH} "Installs QUEDICHO, the .NET runtime, and the local Whisper model."
LangString DESC_SecDesktopShortcut ${LANG_SPANISH} "Crea un acceso directo para todos los usuarios."
LangString DESC_SecDesktopShortcut ${LANG_ENGLISH} "Creates a desktop shortcut for all users."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} $(DESC_SecMain)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktopShortcut} $(DESC_SecDesktopShortcut)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function un.onInit
  SetShellVarContext all
FunctionEnd

Section "Uninstall"
  FindWindow $0 "" "QUEDICHO — Transcripción local"
  IntCmp $0 0 applicationClosed
  MessageBox MB_OK|MB_ICONSTOP "Cierra QUEDICHO antes de desinstalarlo."
  Abort

applicationClosed:
  IfFileExists "$INSTDIR\.quedicho-install" markerPresent
  MessageBox MB_OK|MB_ICONSTOP "No se encontró el marcador de instalación. No se eliminaron archivos."
  Abort

markerPresent:
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\SoftHolding\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\SoftHolding"

  SetRegView 64
  DeleteRegKey HKLM "${APP_REGISTRY_KEY}"
  DeleteRegKey HKLM "${UNINSTALL_REGISTRY_KEY}"

  RMDir /r "$INSTDIR"
SectionEnd
