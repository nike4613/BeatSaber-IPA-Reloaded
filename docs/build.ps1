if ($PSEdition -eq "Core") {
    Write-Error "Build must be run with Windows PowerShell due to the use of CodeDOM"
    Write-Output "Running with Windows PowerShell"
    powershell.exe "$(Get-Location)\build.ps1"
    return
}

# read SelfConfig, remove wierd bits, load it, load Newtonsoft, and turn it into a schema

$newtonsoftVer = "13.0.1"
$newtonsoftSchemaVer = "3.0.15-beta2"
$codeDomProviderVer = "3.6.0"
$roslynVer = "3.10.0"
$nugetBase = "$(Get-Location)/nuget"
$newtonsoftLoc = "$nugetBase/Newtonsoft.Json.$newtonsoftVer/lib/netstandard2.0/Newtonsoft.Json.dll"
$newtonsoftSchemaLoc = "$nugetBase/Newtonsoft.Json.Schema.$newtonsoftSchemaVer/lib/netstandard2.0/Newtonsoft.Json.Schema.dll"
$roslynCodeDomBase = "$nugetBase/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.$codeDomProviderVer"
$roslynCodeDom = "$roslynCodeDomBase/lib/net45/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll"
$roslynBase = "$nugetBase/Microsoft.Net.Compilers.Toolset.$roslynVer"
$roslynInstall = "$roslynBase/tasks/net472/"
$selfConfigLoc = "../IPA.Loader/Config/SelfConfig.cs"
$ipaRoot = "../IPA"

if (!(Test-Path "nuget" -PathType Container)) {
    $nugetExe = "nuget/nuget.exe"
    mkdir "nuget"
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetExe

    &$nugetExe install Newtonsoft.Json -Version $newtonsoftVer -source https://api.nuget.org/v3/index.json -o $nugetBase
    &$nugetExe install Newtonsoft.Json.Schema -Version $newtonsoftSchemaVer -source https://api.nuget.org/v3/index.json -o $nugetBase
    &$nugetExe install Microsoft.CodeDom.Providers.DotNetCompilerPlatform -Version $codeDomProviderVer -source https://api.nuget.org/v3/index.json -o $nugetBase
    &$nugetExe install Microsoft.Net.Compilers.Toolset -Version $roslynVer -source https://api.nuget.org/v3/index.json -o $nugetBase
}

& docfx metadata

if ((Test-Path $roslynCodeDom -PathType Leaf) -and (Test-Path $roslynInstall -PathType Container)) {
    # The files we need exist, lets do this!

    Write-Output "Generating Schema JSON"

    # Add the Roslyn CodeDom
    Add-Type -Path $roslynCodeDom

    # First load Newtonsoft
    Add-Type -Path $newtonsoftLoc
    Add-Type -Path $newtonsoftSchemaLoc

    # Read and parse special directives from SelfConfig
    function ProcessLines {
        begin {
            $inIgnoreSection = $false
            $ignoreNext = 0
        }
        process {
            if ( $_ -match "^\s*//\s+([A-Z]+):\s*(?:section)?\s+([a-zA-Z]+)(?:\s+(\d+))?\s*$" ) {
                $Begin = ($Matches[1] -eq "BEGIN")
                $End = ($Matches[1] -eq "END")
                $Line = ($Matches[1] -eq "LINE")
                $Num = if ($Matches[3].length -gt 0) { [int]($Matches[3]) } else { 1 }

                switch ($Matches[2]) {
                    "ignore" { 
                        if ($Begin) { $inIgnoreSection = $true }
                        if ($End) { $inIgnoreSection = $false }
                        if ($Line) { $ignoreNext = $Num + 1 }
                    }
                }
            }

            if ($inIgnoreSection) { "" }
            elseif ($ignoreNext -gt 0) { $ignoreNext = $ignoreNext - 1; return "" }
            else { $_ }
        }
    }

    function Merge-Lines {
        begin { $str = "" }
        process { $str = $str + "`n" + $_ }
        end { $str }
    }

    function FilterDef {
        process { $_ -replace "internal", "public" }
    }

    # set up the compiler settings
    Invoke-Expression -Command @"
class RoslynCompilerSettings : Microsoft.CodeDom.Providers.DotNetCompilerPlatform.ICompilerSettings
{
    [string] get_CompilerFullPath()
    {
        return "$roslynInstall\csc.exe"
    }
    [int] get_CompilerServerTimeToLive()
    {
        return 10
    }
}
"@
    $codeDomProvider = [Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider]::new([RoslynCompilerSettings]::new())

    Add-Type -CodeDomProvider $codeDomProvider -TypeDefinition (Get-Content $selfConfigLoc | ProcessLines | Merge-Lines | FilterDef) -ReferencedAssemblies $newtonsoftLoc,"netstandard"

    # type will be [IPA.Config.SelfConfig]

    # Generate schema
    $schemagen = New-Object -TypeName Newtonsoft.Json.Schema.Generation.JSchemaGenerator
    $schemagen.DefaultRequired = [Newtonsoft.Json.Required]::Always
    $schema = $schemagen.Generate([IPA.Config.SelfConfig])

    $schema.ToString() | Out-File "other_api/config/_schema.json"
} else {
    Write-Output "Cannot generate schema JSON"
}

$ipaExe = "$ipaRoot/bin/Release/net472/IPA.exe"
# generate IPA.exe args file
if (-not (Test-Path $ipaExe -PathType Leaf)) {
    msbuild -t:Restore -p:Configuration=Release -p:Platform=AnyCPU -p:SolutionDir=.. "$ipaRoot/IPA.csproj"
    msbuild -p:Configuration=Release -p:Platform=AnyCPU -p:SolutionDir=.. "$ipaRoot/IPA.csproj"
}

& "$ipaExe" --help > .\articles\_ipa_command_line.txt

& docfx build --globalMetadataFiles link_branch.json @Args
if ($lastexitcode -ne 0) {
    throw [System.Exception] "docfx build failed with exit code $lastexitcode."
}