& docfx metadata

# read SelfConfig, remove wierd bits, load it, load Newtonsoft, and turn it into a schema
$newtonsoftLoc = "nuget/Newtonsoft.Json.12.0.2/lib/netstandard2.0/Newtonsoft.Json.dll"
$newtonsoftSchemaLoc = "nuget/Newtonsoft.Json.Schema.3.0.11/lib/netstandard2.0/Newtonsoft.Json.Schema.dll"
$selfConfigLoc = "../IPA.Loader/Config/SelfConfig.cs"

if (!(Test-Path "nuget" -PathType Container)) {
    nuget install Newtonsoft.Json -Version 12.0.2 -source https://api.nuget.org/v3/index.json -o nuget
    nuget install Newtonsoft.Json.Schema -Version 3.0.11 -source https://api.nuget.org/v3/index.json -o nuget
}

if ((Test-Path $newtonsoftLoc -PathType Leaf) -and (Test-Path $selfConfigLoc -PathType Leaf)) {
    # The files we need exist, lets do this!

    # First load Newtonsoft
    Add-Type -Path $newtonsoftLoc
    Add-Type -Path $newtonsoftSchemaLoc

    # Read and parse special directives from SelfConfig
    function Process-Lines {
        begin {
            $inIgnoreSection = $false
        }
        process {
            if ( $_ -match "^\s*//\s+([A-Z]+):\s+section\s+(.+)\s*$" ) {
                $Begin = ($Matches[1] -eq "BEGIN")
                $End = ($Matches[1] -eq "END")
                switch ($Matches[2]) {
                    "ignore" { 
                        if ($Begin) { $inIgnoreSection = $true }
                        if ($End) { $inIgnoreSection = $false }
                    }
                }
            }

            if ($inIgnoreSection) { "" }
            else { $_ }
        }
    }

    function Merge-Lines {
        begin { $str = "" }
        process { $str = $str + "`n" + $_ }
        end { $str }
    }

    function Filter-Def {
        process { $_ -replace "internal", "public" }
    }

    Add-Type -TypeDefinition (Get-Content $selfConfigLoc | Process-Lines | Merge-Lines | Filter-Def) -ReferencedAssemblies $newtonsoftLoc,"netstandard"

    # type will be [IPA.Config.SelfConfig]

    # Generate schema
    $schemagen = New-Object -TypeName Newtonsoft.Json.Schema.Generation.JSchemaGenerator
    $schema = $schemagen.Generate([IPA.Config.SelfConfig])

    $schema.ToString() | Out-File "other_api/config/_schema.json"
}

& docfx build --globalMetadataFiles link_branch.json @Args
if ($lastexitcode -ne 0) {
    throw [System.Exception] "docfx build failed with exit code $lastexitcode."
}