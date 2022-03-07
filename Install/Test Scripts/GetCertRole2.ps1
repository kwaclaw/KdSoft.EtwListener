$clientCert = Get-PfxCertificate -FilePath './push-agent-site1.p12'
$subjectName = $clientCert.SubjectName.Decode(128)+','
if ($subjectName -match 'OID\.2\.5\.4\.72=([^,]*)') { 
    $role = $Matches[1]
}
Write-Output $role, $clientCert.Thumbprint