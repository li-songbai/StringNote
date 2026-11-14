@echo off
:: StringNote (弦笺) Launcher
chcp 65001 >nul
setlocal DisableDelayedExpansion
set "APP=%~dp0Xianpu.Desktop.exe"
if exist "%APP%" goto launch
set "APP=%~dp0app\Xianpu.Desktop\bin\Release\net10.0-windows\win-x64\publish\Xianpu.Desktop.exe"
if exist "%APP%" goto launch
set "APP=%~dp0app\Xianpu.Desktop\bin\Release\net10.0-windows\publish\Xianpu.Desktop.exe"
if exist "%APP%" goto launch
set "APP=%~dp0app\Xianpu.Desktop\bin\Debug\net10.0-windows\Xianpu.Desktop.exe"
if exist "%APP%" goto launch
echo 弦笺 (StringNote) 主程序 Xianpu.Desktop.exe 未找到。
echo 请完整解压 StringNote-Portable-win-x64.zip 后，再运行目录内的 Start-Xianpu.cmd 或启动弦谱.cmd。
pause
exit /b 1

:launch
if /i "%~1"=="/check" (
    echo Found "%APP%"
    exit /b 0
)
start "" /D "%~dp0" "%APP%"
exit /b 0