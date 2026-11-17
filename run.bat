@echo off
rem Compila (se preciso) e abre o aplicativo.
setlocal
cd /d "%~dp0"
set "DOTNET=dotnet"
where dotnet >nul 2>nul || set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
"%DOTNET%" run --project src\AcessivelSsh.App\AcessivelSsh.App.csproj -c Release
exit /b %ERRORLEVEL%
