@echo off
rem Gera a pasta publish\ com o app self-contained (roda sem .NET instalado).
setlocal
cd /d "%~dp0"
set "DOTNET=dotnet"
where dotnet >nul 2>nul || set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
"%DOTNET%" publish src\AcessivelSsh.App\AcessivelSsh.App.csproj -c Release -r win-x64 --self-contained true -o publish
exit /b %ERRORLEVEL%
