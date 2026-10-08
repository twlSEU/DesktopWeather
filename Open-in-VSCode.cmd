@echo off
set "DOTNET_ROOT=%~dp0.tools\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
code.cmd --new-window "%~dp0." "%~dp0MainWindow.xaml"
