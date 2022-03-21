param(
    [Parameter(mandatory=$true)][String]$sourceDir,
    [Parameter(mandatory=$true)][String]$targetDir,
    [Parameter(mandatory=$true)][String]$file,
    [Parameter(mandatory=$true)][String]$user,
    [String]$pwd
)

[string] $serviceName = "EtwAgentManager"
[string] $serviceDescription = "Manages ETW Push Agents"
[string] $serviceDisplayName = "Etw Agent Manager"
[string] $appSettingsFile = [System.IO.Path]::Combine($sourceDirPath, "appsettings.json")

################# Functions ##################

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

function Import-Cert {
    param (
        [string] $serverCertFile,
        [string] $storeLocation
    )

    $cred = Get-Credential -UserName 'Installer' -Message ('Password for ' + $serverCertFile)
    if ($cred) {
        $serverCert = Import-PfxCertificate -FilePath $serverCertFile -CertStoreLocation $storeLocation -Password $cred.Password
    }

    Write-Output $serverCert
}

################ End Functions #################

$sourceDirPath = [System.IO.Path]::GetFullPath($sourceDir)
$targetDirPath = [System.IO.Path]::GetFullPath($targetDir)

# install root certificate
Write-Host Importing root certificate
$rootCertPath = [System.IO.Path]::Combine($sourceDirPath, "Kd-Soft.cer")
Import-Certificate -FilePath $rootCertPath -CertStoreLocation Cert:\LocalMachine\Root

# process first server certificate
Write-Host
Write-Host Checking server certificates
$noClientCert = $true
foreach ($serverCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    $serverCert = Import-Cert $serverCertFile cert:\localMachine\my
    if ($serverCert) {
        Write-Host Using server certificate $serverCertFile

        [PSCustomObject] $jsonObject = Load-JsonObject $appSettingsFile
        $httpsObject = $jsonObject.Kestrel.Endpoints.Https
        $httpsObject.psobject.properties.remove('Path')
        $httpsObject.psobject.properties.remove('Password')
        $httpsObject | Add-Member -Force -MemberType NoteProperty -Name 'Store' -Value 'My'
        $httpsObject | Add-Member -Force -MemberType NoteProperty -Name 'Location' -Value 'LocalMachine'
        $httpsObject | Add-Member -Force -MemberType NoteProperty -Name 'Thumbprint' -Value $serverCert.ThumbPrint
        Save-JsonObject $appSettingsFile $jsonObject

        Write-Host 'Processed' $serverCertFile
        break
    }
}

# if password is empty, create a dummy one to allow credentials for system accounts: 
#NT AUTHORITY\LOCAL SERVICE
#NT AUTHORITY\NETWORK SERVICE
if ($pwd -eq "") {
    $secpwd = (new-object System.Security.SecureString)
}
else {
    $secpwd = ConvertTo-SecureString $pwd -AsPlainText -Force
}
$cred = New-Object System.Management.Automation.PSCredential ($user, $secpwd)

# make sure $targetDirPath exists
if (!(test-path $targetDirPath)) {
  New-Item -ItemType "directory" -Path $targetDirPath
}

# LocalSystem already has all kinds of permissions
if ($cred.UserName -notlike '*\LocalSystem') {
    $acl = Get-Acl "$targetDirPath"
    #$aclRuleArgs = $cred, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($user, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule)
    $acl | Set-Acl "$targetDirPath"
}


$existingService  = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
if ($existingService) {
  "'$serviceName' exists already. Stopping."
  Stop-Service $serviceName
  "Waiting 3 seconds to allow existing service to stop."
  Start-Sleep -s 3
    
  $existingService.Delete()
  "Waiting 5 seconds to allow service to be uninstalled."
  Start-Sleep -s 5  
}
  
echo "Install Directory: $targetDirPath"

# delete target bin directory
$binPath = [System.IO.Path]::Combine($targetDirPath, "bin")
Remove-Item $binPath -Recurse -ErrorAction SilentlyContinue

#copy source publish directory to target bin directory
$sourceBinPath = [System.IO.Path]::Combine($sourceDirPath, "publish") 
Copy-Item -Path $sourceBinPath -Destination $binPath -Recurse

#path of service binary executable
$filepath = [System.IO.Path]::Combine($binPath, $file)

"Installing the service."
New-Service -Name $serviceName -BinaryPathName $filepath -Credential $cred -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic

"Configuring the service"
sc.exe failure $serviceName reset= 86400 actions= restart/6000/restart/6000/restart/6000

"Installed and configured the service."

#$ShouldStartService = Read-Host "Would you like the '$serviceName ' service started? Y or N"
#if($ShouldStartService -eq "Y") {
    "Starting the service."
    Start-Service $serviceName
#}
"Completed."
