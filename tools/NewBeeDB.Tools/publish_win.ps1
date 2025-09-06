dotnet publish  -r win-x64 -c Release -p:PublishAot=true -o ../../dist/tools

function DeletePdb {
    param(
        [string]$dllName
    )

    $pdbPath = "../../dist/tools/$dllName.pdb"

    if (Test-Path $pdbPath) {
        Remove-Item $pdbPath
    }
}

DeletePdb("nbdb")
DeletePdb("NewBeeDB")
DeletePdb("NewBeeDB.Backends")