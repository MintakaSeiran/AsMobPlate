param(
    [string]$DotnetPath = 'dotnet',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$hasSdk = $false
if (Get-Command $DotnetPath -ErrorAction SilentlyContinue) {
    $hasSdk = @(& $DotnetPath --list-sdks) -match '^10\.'
}
if (-not $hasSdk) {
    $bundledDotnet = Join-Path $env:LOCALAPPDATA 'AsMobPlateBuild\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $bundledDotnet)) { throw 'Install .NET 10 or supply -DotnetPath.' }
    $DotnetPath = $bundledDotnet
}

Push-Location $repoRoot
try {
    foreach ($project in @('tests/AsMobPlate.Timing.Tests', 'tests/AsMobPlate.Rendering.Tests')) {
        & $DotnetPath run --project $project
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
    }
    & $DotnetPath build AsMobPlate.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    $buildDir = Join-Path $repoRoot 'bin\Release'
    $dllVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $buildDir 'AsMobPlate.dll')).Version.ToString()
    $manifest = Get-Content (Join-Path $buildDir 'AsMobPlate.json') -Raw | ConvertFrom-Json
    $projectVersion = ([xml](Get-Content 'AsMobPlate.csproj' -Raw)).Project.PropertyGroup.Version
    $expectedVersion = ([version]$projectVersion).ToString(3) + '.0'
    if ($manifest.AssemblyVersion -ne $dllVersion -or $dllVersion -ne $expectedVersion) {
        throw 'Project, DLL and manifest versions do not match.'
    }

    $packageDir = Join-Path $repoRoot "output\packages\$dllVersion"
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    $package = Join-Path $packageDir "AsMobPlate-$dllVersion.zip"
    $staging = Join-Path $repoRoot ('output\staging\' + [guid]::NewGuid().ToString('N'))
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $buildDir 'AsMobPlate\latest.zip'), $staging)
    Copy-Item -LiteralPath (Join-Path $buildDir 'images') -Destination $staging -Recurse
    foreach ($required in @('AsMobPlate.dll', 'AsMobPlate.json', 'AsMobPlate.deps.json',
        'System.Speech.dll', 'runtimes\win\lib\net9.0\System.Speech.dll', 'images\icon.png', 'images\image1.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $staging $required))) { throw "Package missing $required" }
    }
    $packedVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $staging 'AsMobPlate.dll')).Version.ToString()
    $packedManifest = Get-Content (Join-Path $staging 'AsMobPlate.json') -Raw | ConvertFrom-Json
    if ($packedVersion -ne $dllVersion -or $packedManifest.AssemblyVersion -ne $dllVersion) {
        throw 'Packaged DLL or manifest version mismatch.'
    }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $package -Force
    $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($package))" | Set-Content -LiteralPath ($package + '.sha256') -Encoding ascii
    Write-Output "Package: $package"
    Write-Output "SHA256: $hash"

    if ($Deploy) {
        $destination = Join-Path $env:APPDATA 'XIVLauncher\devPlugins\AsMobPlate'
        if (Test-Path -LiteralPath $destination) {
            $backup = Join-Path $repoRoot ('output\dev-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
            Copy-Item -LiteralPath $destination -Destination $backup -Recurse
            Write-Output "Backup: $backup"
        }
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Get-ChildItem -LiteralPath $staging | Copy-Item -Destination $destination -Recurse -Force
        foreach ($file in Get-ChildItem -LiteralPath $staging -File -Recurse) {
            $relative = [IO.Path]::GetRelativePath($staging, $file.FullName)
            $target = Join-Path $destination $relative
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                throw "Deployment verification failed: $relative"
            }
        }
        Write-Output "Deployed $dllVersion to $destination. Reload the dev plugin in Dalamud to activate it."
    }
}
finally { Pop-Location }
