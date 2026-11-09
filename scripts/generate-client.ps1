# Generates ApexPost.Client/Generated/ApexPostClient.g.cs from contracts/v2.json with NSwag.
# This script IS the committed command. Run scripts\export-contracts.ps1 first.
# The generated file is never edited by hand; extensions go in partial classes or wrappers.
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
Push-Location $root
try {
    # NSwag.ConsoleCore is pinned in .config/dotnet-tools.json. Allow it to run on a newer .NET runtime.
    $env:DOTNET_ROLL_FORWARD = "LatestMajor"
    dotnet tool restore

    dotnet nswag openapi2csclient `
        /input:contracts/v2.json `
        /output:ApexPost.Client/Generated/ApexPostClient.g.cs `
        /namespace:ApexPost.Client `
        /className:ApexPostClient `
        /operationGenerationMode:SingleClientFromOperationId `
        /generateClientInterfaces:true `
        /injectHttpClient:true `
        /disposeHttpClient:false `
        /useBaseUrl:false `
        /jsonLibrary:SystemTextJson `
        /generateNullableReferenceTypes:true `
        /generateOptionalParameters:false `
        /classStyle:Poco `
        /generateExceptionClasses:true `
        /exceptionClass:ApiException
}
finally { Pop-Location }
