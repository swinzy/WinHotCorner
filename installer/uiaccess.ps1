# Signs the hot corner with a certificate made on this computer, so that its uiAccess version can start (see
# "Startup and privileges" in docs\technical.md). Run elevated by the hot corner installer and uninstaller.
#
#   -Sign <file>          Makes a certificate for code signing only, trusts it on this computer, signs the file with
#                         it and deletes the private key. Prints "Thumbprint=<thumbprint>" of the new certificate.
#   -Remove [-Keep <tp>]  Removes the certificates this script made, except the one with thumbprint <tp>.
#   -Remove -Only <tp>    Removes only that one.
#
# The private key is deleted straight after signing, so nothing else can ever be signed with the certificate: trusting
# it trusts only this one file. A signature without a timestamp is valid only while its certificate is, so the
# certificate lasts 100 years.
param([string]$Sign, [switch]$Remove, [string]$Keep = '', [string]$Only = '')
$ErrorActionPreference = 'Stop'

$subject = 'CN=WinHotCorner (made on this computer)'

function Remove-Certificates {
    Get-ChildItem Cert:\LocalMachine\Root | Where-Object {
        $_.Subject -eq $subject -and $_.Thumbprint -ne $Keep -and ($Only -eq '' -or $_.Thumbprint -eq $Only)
    } | ForEach-Object {
        Remove-Item $_.PSPath
        "Removed certificate $($_.Thumbprint)"
    }
}

if ($Remove) {
    Remove-Certificates
    exit 0
}

if (-not (Test-Path -LiteralPath $Sign)) { throw "Not found: $Sign" }

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation Cert:\LocalMachine\My `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(100)
$trusted = $false
try {
    # Trusted first: Set-AuthenticodeSignature checks the chain
    $root = New-Object System.Security.Cryptography.X509Certificates.X509Store 'Root', 'LocalMachine'
    $root.Open('ReadWrite')
    try {
        $root.Add((New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 (, $cert.RawData)))
    }
    finally {
        $root.Close()
    }
    $trusted = $true

    $signature = Set-AuthenticodeSignature -LiteralPath $Sign -Certificate $cert -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') { throw "Signing failed: $($signature.Status) $($signature.StatusMessage)" }
    "Thumbprint=$($cert.Thumbprint)"
}
catch {
    if ($trusted) { Remove-Item "Cert:\LocalMachine\Root\$($cert.Thumbprint)" }
    throw
}
finally {
    # The certificate with its private key: only the copy in Root is kept
    Remove-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" -DeleteKey
}
