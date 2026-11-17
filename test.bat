@echo off
rem Roda os testes unitarios (xUnit).
setlocal
cd /d "%~dp0"
set "DOTNET=dotnet"
where dotnet >nul 2>nul || set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
"%DOTNET%" test AcessivelSsh.slnx -c Release
exit /b %ERRORLEVEL%
