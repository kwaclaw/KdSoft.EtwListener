#Requires -Version 5.1

$rootDevCertPath = './EtwEvents.PushAgent/certificates/Kd-Soft.cer'
$managerDevCertPath = './EtwEvents.AgentManager/certificates/Dev Admin.p12'
$agentDevCertPath = './EtwEvents.PushAgent/certificates/my-dev-site.p12'

# install root/signing certificate
Write-Host Importing development root certificate
Import-Certificate -FilePath $rootDevCertPath -CertStoreLocation Cert:\LocalMachine\Root

# import client certificates (no password)
Write-Host
Write-Host Importing development manager certificate
Import-PfxCertificate -FilePath $managerDevCertPath -CertStoreLocation Cert:\CurrentUser\my
Write-Host Imported certificate $managerDevCertPath
Write-Host
Write-Host Importing development agent certificate
Import-PfxCertificate -FilePath $agentDevCertPath -CertStoreLocation Cert:\LocalMachine\my
Write-Host Imported certificate $agentDevCertPath

Write-Host
Read-Host -Prompt "Press Enter to continue"