# ApexPost Counter Platform: status

All layers are written, but NOT yet compiled by the author (no .NET SDK was available when the code was written).
The first build on a machine with the .NET 10 SDK may show a few errors: fix them, then follow the steps below.

## Written
- Domain, Application, Infrastructure (built successfully earlier on the VM)
- Web: Program.cs, composition, 7 middleware, 5 filters, 13 controllers, Razor views, css, appsettings
- UnitTests (Domain, Services, Rules, Infrastructure) and IntegrationTests (Middleware, Security, Authentication, Filters)
- README.md, DESIGN.md (ADRs are drafts: rewrite in your own words), AUDIT-SAMPLE.json (illustrative; replace with real output), docs/EVIDENCE.md
- setup-secrets.bat, publish.bat, run-published.bat

## Do next
1. `dotnet build ApexPost.slnx` and fix errors. When the build is clean, delete the line
   `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` from ApexPost.Web, ApexPost.UnitTests and ApexPost.IntegrationTests
   so the zero-warning rule applies to every project, then build again.
2. `setup-secrets.bat`, then run the app and click through every screen as each role.
3. `dotnet test ApexPost.slnx`
4. Replace AUDIT-SAMPLE.json with real output (sign in as an auditor, open /Audit/Export, save the JSON).
5. Fill in docs/EVIDENCE.md, the measurements in DESIGN.md section 8 and the Week 1 comparison in section 1.
6. Commit on a feature branch and open a pull request. Never commit `data/`, user-secrets or API keys.
