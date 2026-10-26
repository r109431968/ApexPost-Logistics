@echo off
rem Builds a self-contained Windows program (no .NET install needed on the target PC).
rem Output: publish\ApexPost.Web.exe  (start it with run-published.bat)
setlocal
dotnet publish ApexPost.Web\ApexPost.Web.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
if errorlevel 1 (
  echo Publish failed. Fix the build errors first: dotnet build ApexPost.slnx
  exit /b 1
)
echo.
echo Published to the "publish" folder. Run run-published.bat to start it.
endlocal
