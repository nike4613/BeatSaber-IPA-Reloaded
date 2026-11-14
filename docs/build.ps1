if ($PSEdition -eq "Core") {
    Write-Error "Build must be run with Windows PowerShell due to a System.Text.Json conflict with PowerShell Core's own"
    Write-Output "Running with Windows PowerShell"
    powershell.exe "$(Get-Location)\build.ps1"
    return
}

# build IPA.Loader, load it, load System.Text.Json, and turn SelfConfig into a schema

$loaderProj = "../IPA.Loader/IPA.Loader.csproj"
$loaderOut = "../IPA.Loader/bin/Release/net472"
$loaderDll = "$loaderOut/IPA.Loader.dll"

& docfx metadata

if (-not (Test-Path $loaderDll -PathType Leaf)) {
    msbuild -t:Restore -p:Configuration=Release -p:Platform=AnyCPU $loaderProj
    msbuild -p:Configuration=Release -p:Platform=AnyCPU -p:RunAnalyzersDuringBuild=false $loaderProj
}

if (Test-Path $loaderDll -PathType Leaf) {
    # The files we need exist, lets do this!

    Write-Output "Generating Schema JSON"

    # First load IPA.Loader, then System.Text.Json
    $loader = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $loaderDll))
    $null = [System.Reflection.Assembly]::LoadFrom((Resolve-Path "$loaderOut/System.Text.Json.dll"))

    # type will be [IPA.Config.SelfConfig]

    # Generate schema
    $exporterOptions = [System.Text.Json.Schema.JsonSchemaExporterOptions]::new()
    $exporterOptions.TreatNullObliviousAsNonNullable = $true
    $selfConfigType = $loader.GetType("IPA.Config.SelfConfig", $true)
    $schema = [System.Text.Json.Schema.JsonSchemaExporter]::GetJsonSchemaAsNode([System.Text.Json.JsonSerializerOptions]::Default, $selfConfigType, $exporterOptions)

    $schema.ToString() | Out-File "other_api/config/_schema.json"
} else {
    Write-Output "Cannot generate schema JSON"
}

& docfx build --globalMetadataFiles link_branch.json @Args
if ($lastexitcode -ne 0) {
    throw [System.Exception] "docfx build failed with exit code $lastexitcode."
}
