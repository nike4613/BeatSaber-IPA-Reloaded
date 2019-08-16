# read SelfConfig, remove wierd bits, load it, load Newtonsoft, and turn it into a schema
$newtonsoftLoc = "$(Get-Location)/nuget/Newtonsoft.Json.12.0.2/lib/netstandard2.0/Newtonsoft.Json.dll"
$newtonsoftSchemaLoc = "$(Get-Location)/nuget/Newtonsoft.Json.Schema.3.0.11/lib/netstandard2.0/Newtonsoft.Json.Schema.dll"
$selfConfigLoc = "../IPA.Loader/Config/SelfConfig.cs"

if (!(Test-Path "nuget" -PathType Container)) {
    nuget install Newtonsoft.Json -Version 12.0.2 -source https://api.nuget.org/v3/index.json -o "$(Get-Location)/nuget"
    nuget install Newtonsoft.Json.Schema -Version 3.0.11 -source https://api.nuget.org/v3/index.json -o "$(Get-Location)/nuget"
}

& docfx metadata

if ((Test-Path $newtonsoftLoc -PathType Leaf) -and (Test-Path $selfConfigLoc -PathType Leaf)) {
    # The files we need exist, lets do this!

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

    Add-Type -TypeDefinition (Get-Content $selfConfigLoc | ProcessLines | Merge-Lines | FilterDef) -ReferencedAssemblies $newtonsoftLoc,"netstandard"

    # type will be [IPA.Config.SelfConfig]

    # Generate schema
    $schemagen = New-Object -TypeName Newtonsoft.Json.Schema.Generation.JSchemaGenerator
    $schemagen.DefaultRequired = [Newtonsoft.Json.Required]::Always
    $schema = $schemagen.Generate([IPA.Config.SelfConfig])

    $schema.ToString() | Out-File "other_api/config/_schema.json"
}

& docfx build --globalMetadataFiles link_branch.json @Args
if ($lastexitcode -ne 0) {
    throw [System.Exception] "docfx build failed with exit code $lastexitcode."
}