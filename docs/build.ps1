# read SelfConfig, remove wierd bits, load it, load Newtonsoft, and turn it into a schema
$newtonsoftLoc = "$(Get-Location)/nuget/Newtonsoft.Json.12.0.2/lib/netstandard2.0/Newtonsoft.Json.dll"
$newtonsoftSchemaLoc = "$(Get-Location)/nuget/Newtonsoft.Json.Schema.3.0.11/lib/netstandard2.0/Newtonsoft.Json.Schema.dll"
$roslynCodeDomBase = "$(Get-Location)/nuget/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.2.0.1"
$roslynCodeDom = "$roslynCodeDomBase/lib/net45/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll"
$selfConfigLoc = "../IPA.Loader/Config/SelfConfig.cs"
$ipaRoot = "../IPA"

if (!(Test-Path "nuget" -PathType Container)) {
    nuget install Newtonsoft.Json -Version 12.0.2 -source https://api.nuget.org/v3/index.json -o "$(Get-Location)/nuget"
    nuget install Newtonsoft.Json.Schema -Version 3.0.11 -source https://api.nuget.org/v3/index.json -o "$(Get-Location)/nuget"
    nuget install Microsoft.CodeDom.Providers.DotNetCompilerPlatform -Version 2.0.1 -source https://api.nuget.org/v3/index.json -o "$(Get-Location)/nuget"
}

& docfx metadata

if ((Test-Path $newtonsoftLoc -PathType Leaf) -and (Test-Path $selfConfigLoc -PathType Leaf) -and (Test-Path $roslynCodeDom -PathType Leaf)) {
    # The files we need exist, lets do this!

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
        return "$roslynCodeDomBase\tools\RoslynLatest\csc.exe"
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
}

$ipaExe = "$ipaRoot/bin/Release/IPA.exe"
# generate IPA.exe args file
if (-not (Test-Path $ipaExe -PathType Leaf)) {
    msbuild -p:Configuration=Release -p:Platform=AnyCPU -p:SolutionDir=.. "$ipaRoot/IPA.csproj"
}

& "$ipaExe" --help > .\articles\_ipa_command_line.txt

& docfx build --globalMetadataFiles link_branch.json @Args
if ($lastexitcode -ne 0) {
    throw [System.Exception] "docfx build failed with exit code $lastexitcode."
}