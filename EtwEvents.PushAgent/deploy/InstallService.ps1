param(
    [Parameter(mandatory=$true)][String]$sourceDir,
    [Parameter(mandatory=$true)][String]$targetDir,
    [Parameter(mandatory=$true)][String]$file,
    [Parameter(mandatory=$true)][String]$user,
    [String]$managerUrl,
    [String]$pwd
)

[string] $serviceName = "EtwPushAgent"
[string] $serviceDescription = "Forwards ETW Events to Event Sink"
[string] $serviceDisplayName = "Etw PushAgent"
[string] $appSettingsFile = './appSettings.Local.json'

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

function Get-CertPfxItems {
    param (
        [string] $certFilePath
    )

    $clientCert = Get-PfxCertificate -FilePath $certFilePath
    $subjectName = $clientCert.SubjectName.Decode(128)
    if ($subjectName -match 'OID\.2\.5\.4\.72=([^,]*)') { 
        $role = $Matches[1]
    }

    Write-Output $role, $clientCert
}

function Add-Cert {
    param (
        [string] $storeName,
        [string] $storeLocation,
        $cert
    )

    try {
        $store =  New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Store -ArgumentList $storeName, $storeLocation 
        $store.Open(1)
        $store.Add($cert)
    }
    finally {
        $store.Close()
    }
}

################ End Functions #################

#Write-Host $targetDir
#Write-Host $managerUrl

$sourceDirPath = [System.IO.Path]::GetFullPath($sourceDir)
$targetDirPath = [System.IO.Path]::GetFullPath($targetDir)

# save manager URL
if ($managerUrl) {
    [PSCustomObject] $jsonObject = Load-JsonObject $appSettingsFile
    $jsonObject.Control | Add-Member -Force -MemberType NoteProperty -Name 'Uri' -Value $managerUrl
    Save-JsonObject $appSettingsFile $jsonObject
}

# install root certificate
Write-Host Importing root certificate
$rootCertPath = [System.IO.Path]::Combine($sourceDirPath, "Kd-Soft.cer")
Import-Certificate -FilePath $rootCertPath -CertStoreLocation Cert:\LocalMachine\Root

# process first client certificate matching role=etw-pushagent
Write-Host
Write-Host Checking client certificates
$noClientCert = $true
foreach ($clientCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    [string] $role, $clientCert = Get-CertPfxItems $clientCertFile

    if ($role -eq 'etw-pushagent') {
        Write-Host Importing client certificate $clientCertFile
        Add-Cert 'My' 'LocalMachine' $clientCert

        [PSCustomObject] $jsonObject = Load-JsonObject $appSettingsFile
        #$jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'SubjectRole' -Value $role
        $jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'Thumbprint' -Value $clientCert.ThumbPrint
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

# copy local appsettings file to target directory if it does not exist already
$localSettingsPath = [System.IO.Path]::Combine($targetDirPath, "appsettings.Local.json") 
if (!(test-path $localSettingsPath)) {
  $sourceSettingsPath = [System.IO.Path]::Combine($sourceDirPath, "appsettings.Local.json")
  Copy-Item $sourceSettingsPath -Destination $targetDirPath -ErrorAction SilentlyContinue
}

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
