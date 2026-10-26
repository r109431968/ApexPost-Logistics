@echo off
rem Stores the demo password and the partner API key in user-secrets (outside the repository).
rem Run once from the folder that contains ApexPost.slnx.
setlocal
set /p DEMOPW=Choose a demo password for all seeded accounts (min 12 chars):
set /p APIKEY=Choose an API key for the courier partner (long random text):
dotnet user-secrets set "Seed:DemoPassword" "%DEMOPW%" --project ApexPost.Web
dotnet user-secrets set "Integration:ApiKeys:CourierPartner" "%APIKEY%" --project ApexPost.Web
echo.
echo Done. Delete the folder ApexPost.Web\data if the app was already started once without a demo password.
endlocal
