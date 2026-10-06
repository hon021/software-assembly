param()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$profile = Get-Content (Join-Path $root 'profiles/dotnet-angular/2.0.2/profile.json') -Raw | ConvertFrom-Json
$tools = Join-Path $root '.tools'
$archiveName = "node-v$($profile.toolchain.node)-win-x64.zip"
$nodeDirectory = Join-Path $tools "node-v$($profile.toolchain.node)-win-x64"
$node = Join-Path $nodeDirectory 'node.exe'
$originalPath = $env:PATH
$originalProgress = $ProgressPreference

try {
    $ProgressPreference = 'SilentlyContinue'
    New-Item -ItemType Directory -Path $tools -Force | Out-Null
    if (!(Test-Path $node)) {
        $release = "https://nodejs.org/dist/v$($profile.toolchain.node)"
        $archive = Join-Path $tools $archiveName
        $checksums = (Invoke-WebRequest "$release/SHASUMS256.txt" -UseBasicParsing).Content
        $checksumLine = ($checksums -split "`n") | Where-Object { $_.Trim().EndsWith("  $archiveName", [StringComparison]::Ordinal) }
        if (@($checksumLine).Count -ne 1) { throw 'Node archive checksum could not be resolved.' }
        $expectedHash = ($checksumLine.Trim() -split '\s+')[0]
        Invoke-WebRequest "$release/$archiveName" -OutFile $archive -UseBasicParsing
        if ((Get-FileHash $archive -Algorithm SHA256).Hash -ine $expectedHash) { throw 'Node archive checksum mismatch.' }
        Expand-Archive $archive -DestinationPath $tools -Force
    }

    $actualNode = & $node --version
    if ($LASTEXITCODE -ne 0 -or $actualNode -ne "v$($profile.toolchain.node)") { throw 'Portable Node version mismatch.' }
    $env:PATH = "$nodeDirectory;$originalPath"
    $pnpmDirectory = Join-Path $tools 'pnpm'
    $pnpm = Join-Path $pnpmDirectory 'node_modules/pnpm/bin/pnpm.cjs'
    if (!(Test-Path $pnpm)) {
        & (Join-Path $nodeDirectory 'npm.cmd') install --prefix $pnpmDirectory --no-audit --no-fund "pnpm@$($profile.toolchain.pnpm)"
        if ($LASTEXITCODE -ne 0) { throw 'Portable pnpm install failed.' }
    }
    $actualPnpm = & $node $pnpm --version
    if ($LASTEXITCODE -ne 0 -or $actualPnpm -ne $profile.toolchain.pnpm) { throw 'Portable pnpm version mismatch.' }
    Write-Output "Portable tools ready: Node $actualNode; pnpm $actualPnpm."
}
finally {
    $env:PATH = $originalPath
    $ProgressPreference = $originalProgress
}