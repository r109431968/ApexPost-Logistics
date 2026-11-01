@echo off
rem Stores the database connection string (Windows authentication, no password), the demo password and the partner API key
rem in user-secrets (outside the repository).
rem Run once from the folder that contains ApexPost.slnx.
setlocal
set /p DEMOPW=Choose a demo password for all seeded accounts (min 12 chars):
set /p APIKEY=Choose an API key for the courier partner (long random text):
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project ApexPost.Web
dotnet user-secrets set "Seed:DemoPassword" "%DEMOPW%" --project ApexPost.Web
dotnet user-secrets set "Integration:ApiKeys:CourierPartner" "%APIKEY%" --project ApexPost.Web
echo.
echo Done. If the app was already started once without a demo password, drop the database (dotnet ef database drop) and start again.
endlocal
