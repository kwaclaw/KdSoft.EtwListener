
[string] $appSettingsFile = './appSettings.Local.json'
#[string] $clientCertFile = './push-agent-site1.p12'

$noClientCert = $true
foreach ($clientCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    [string] $role, [string] $thumbPrint = Get-CertPfxItems $clientCertFile

    if ($role -eq 'etw-pushagent') {
        Import-PfxCertificate -FilePath $clientCertFile -CertStoreLocation Cert:\LocalMachine\My

        [PSCustomObject] $jsonObject = Load-JsonObject $appSettingsFile
        #$jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'SubjectRole' -Value $role
        $jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'Thumbprint' -Value $thumbPrint
        Save-JsonObject $appSettingsFile $jsonObject

        $noClientCert = $false
        Write-Host 'Processed' $clientCertFile
        break
    }
}
# if no client certificate is supplied, we add the SubjectRole property so that an already installed certificate can be matched
if ($noClientCert) {
    [PSCustomObject] $jsonObject = Load-JsonObject $appSettingsFile
    $jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'SubjectRole' -Value 'etw-pushagent'
    Save-JsonObject $appSettingsFile $jsonObject
}


function Load-JsonObject {
    param (
        [string] $jsonFilePath
    )

    [string] $content = Get-Content -Raw -Path $jsonFilePath
    [PSCustomObject] $jsonObject = $content | ConvertFrom-Json

    Write-Output $jsonObject
}

function Save-JsonObject {
    param (
        [string] $jsonFilePath,
        [PSCustomObject] $jsonObject
    )

    $jsonObject | ConvertTo-Json -Compress -Depth 6 | Set-Content -Path $jsonFilePath
}

function Get-CertPfxItems {
    param (
        [string] $certFilePath
    )

    $clientCert = Get-PfxCertificate -FilePath $certFilePath
    $subjectName = $clientCert.SubjectName.Decode(128)
    if ($subjectName -match 'OID\.2\.5\.4\.72=([^,]*)') { 
        $role = $Matches[1]
    }

    Write-Output $role, $clientCert.Thumbprint
}
