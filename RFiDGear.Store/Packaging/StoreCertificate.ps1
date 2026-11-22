# Shared by Build-StorePackage.ps1 and Make-StorePackage.ps1 (dot-sourced; nothing runs here).
Set-StrictMode -Version Latest

# What a wrong password, an old certificate or a certificate of the wrong kind would otherwise show only after the build, or only when
# the package is installed. The problems stop the script (no private key, not valid now). The warnings do not: the package is made
# anyway, but it may not install (a certificate that is not meant for code signing, one that this PC does not trust).
function Test-StoreSigningCertificate {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $codeSigning = '1.3.6.1.5.5.7.3.3'
    $problems = @()
    $warnings = @()

    if (-not $Certificate.HasPrivateKey) {
        $problems += 'The certificate has no private key, so it cannot sign.'
    }

    $now = Get-Date
    if ($Certificate.NotBefore -gt $now -or $Certificate.NotAfter -lt $now) {
        $problems += "The certificate is not valid now (valid from $($Certificate.NotBefore.ToString('yyyy-MM-dd')) to $($Certificate.NotAfter.ToString('yyyy-MM-dd')))."
    }

    foreach ($extension in $Certificate.Extensions) {
        if ($extension -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
            $usages = @($extension.EnhancedKeyUsages | ForEach-Object { $_.Value })
            if ($usages -notcontains $codeSigning) {
                $warnings += 'The certificate is not meant for code signing (its extended key usage lacks "Code Signing"): Windows may refuse the package.'
            }
        }

        if ($extension -is [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension] -and $extension.CertificateAuthority) {
            $warnings += 'The certificate is a certificate authority: Windows may refuse a package that it signed. A certificate for signing a package is a leaf.'
        }
    }

    # Windows installs a package whose signer it trusts: the certificate itself is in Trusted People or Trusted Root of the local
    # machine, or its chain ends in a root of the Trusted Root store of the local machine (a certificate that a certificate authority
    # issued is in neither store itself). Revocation is not looked at: it needs the network and says nothing about trust here.
    $trusted = $false
    foreach ($store in 'Cert:\LocalMachine\TrustedPeople', 'Cert:\LocalMachine\Root') {
        if (Test-Path -LiteralPath (Join-Path $store $Certificate.Thumbprint)) {
            $trusted = $true
        }
    }

    if (-not $trusted) {
        $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
        try {
            $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
            [void]$chain.Build($Certificate)
            $root = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate
            $trusted = Test-Path -LiteralPath (Join-Path 'Cert:\LocalMachine\Root' $root.Thumbprint)
        }
        finally {
            $chain.Dispose()
        }
    }

    if (-not $trusted) {
        $warnings += 'This PC does not trust the certificate (it is not in the Trusted People store of the local machine, and it does not chain to a root in the Trusted Root store of the local machine): the package is made, but Windows will not install it until the public certificate (the .cer, not the .pfx) is imported into Trusted People, which needs administrator rights.'
    }

    return [pscustomobject]@{
        Problems = $problems
        Warnings = $warnings
    }
}
