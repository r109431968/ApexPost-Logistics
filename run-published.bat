@echo off
rem Starts the published program. Secrets are typed in here and live only in this window.
setlocal
set /p ASPNETCORE_ENVIRONMENT_CHOICE=Environment (Development/Production) [Development]:
if "%ASPNETCORE_ENVIRONMENT_CHOICE%"=="" set ASPNETCORE_ENVIRONMENT_CHOICE=Development
set ASPNETCORE_ENVIRONMENT=%ASPNETCORE_ENVIRONMENT_CHOICE%
set /p Seed__DemoPassword=Demo password for the seeded accounts (min 12 chars):
set /p Integration__ApiKeys__CourierPartner=API key for the courier partner:
set ASPNETCORE_URLS=https://localhost:7141;http://localhost:5141
echo Open https://localhost:7141 in the browser. Press Ctrl+C to stop.
publish\ApexPost.Web.exe
endlocal
