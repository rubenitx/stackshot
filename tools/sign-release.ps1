# Signs a published release for in-app updates without the private key ever leaving this computer: downloads
# Stackshot.exe from the release, checks it against the published SHA-256, signs it (RSA-PSS, SHA-256) with the local key,
# verifies the signature and uploads only Stackshot.exe.sig with the GitHub CLI.
# Usage: .\tools\sign-release.ps1 v1.3.1 [-KeyPath <file>] [-NoUpload]
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$KeyPath = (Join-Path $env:USERPROFILE '.stackshot-keys\update-signing-key.xml'),
    [switch]$NoUpload
)
$ErrorActionPreference = 'Stop'
if ($Tag -notmatch '^v\d+\.\d+\.\d+$') { throw "Tag must look like v1.2.3: $Tag" }
if (-not (Test-Path -LiteralPath $KeyPath)) { throw "Signing key not found: $KeyPath" }
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$work = Join-Path ([IO.Path]::GetTempPath()) ('stackshot-sign-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $base = "https://github.com/rubenitx/stackshot/releases/download/$Tag"
    $exe = Join-Path $work 'Stackshot.exe'
    $sig = Join-Path $work 'Stackshot.exe.sig'
    Invoke-WebRequest -UseBasicParsing -Uri "$base/Stackshot.exe" -OutFile $exe
    $sumFile = Join-Path $work 'Stackshot.exe.sha256'
    Invoke-WebRequest -UseBasicParsing -Uri "$base/Stackshot.exe.sha256" -OutFile $sumFile
    $published = ([IO.File]::ReadAllText($sumFile).Trim() -split '\s+')[0].ToLowerInvariant()
    $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $published) { throw "SHA-256 mismatch: downloaded $actual, published $published" }

    $rsa = New-Object System.Security.Cryptography.RSACng
    try {
        $rsa.FromXmlString([IO.File]::ReadAllText($KeyPath))
        $data = [IO.File]::ReadAllBytes($exe)
        $sha = [Security.Cryptography.HashAlgorithmName]::SHA256
        $pss = [Security.Cryptography.RSASignaturePadding]::Pss
        $bytes = $rsa.SignData($data, $sha, $pss)
        if (-not $rsa.VerifyData($data, $bytes, $sha, $pss)) { throw 'The new signature does not verify.' }
        [IO.File]::WriteAllText($sig, [Convert]::ToBase64String($bytes))
    }
    finally { $rsa.Dispose() }
    Write-Host "Signed $Tag ($actual)."

    if ($NoUpload) { Copy-Item -LiteralPath $sig -Destination (Get-Location); Write-Host 'Saved Stackshot.exe.sig here (not uploaded).'; return }
    # The GitHub CLI lives in WSL on the maintainer's machine; use it directly if it is installed on Windows.
    if (Get-Command gh -ErrorAction SilentlyContinue) { gh release upload $Tag $sig --clobber -R rubenitx/stackshot }
    else { wsl.exe -e gh release upload $Tag ((wsl.exe -e wslpath -a ($sig -replace '\\', '/')).Trim()) --clobber -R rubenitx/stackshot }
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed.' }
    Write-Host "Uploaded Stackshot.exe.sig to $Tag."
}
finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
