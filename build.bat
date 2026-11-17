@echo off
rem Compila a solucao inteira em Release.
setlocal
cd /d "%~dp0"
set "DOTNET=dotnet"
where dotnet >nul 2>nul || set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
"%DOTNET%" build AcessivelSsh.slnx -c Release
exit /b %ERRORLEVEL%
