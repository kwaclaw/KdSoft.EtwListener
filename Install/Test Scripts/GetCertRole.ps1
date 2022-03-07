$clientCertPfx = Get-PfxData -FilePath './push-agent-site1.p12'
$clientCert = $clientCertPfx.EndEntityCertificates[0]
$subjectName = $clientCert.SubjectName.Decode(128) # + ','
#ConvertTo-Json -InputObject $subjectName | Write-Host
#Write-Host $clientCert.Extensions.Item['OID.2.5.4.72'].ToString()
if ($subjectName -match 'OID\.2\.5\.4\.72=(.*)(,|^,$)') { 
    $role = $Matches[1]
}
Write-Host $role