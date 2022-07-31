param(
    [Parameter(mandatory=$true)][String]$sourceDir,
    [Parameter(mandatory=$true)][String]$targetDir,
    [Parameter(mandatory=$true)][String]$file,
    [String]$serviceName = "EtwPushAgent",
    [String]$serviceDisplayName = "Etw Push Agent",
    [String]$serviceDescription = "Forwards ETW Events to Event Sink",
    [String]$user = '',
    [String]$pwd = '',
    [String]$managerUrl = ''
)

if ($user -eq '') {
    $user = 'NT SERVICE\' + $serviceName
    Write-Host Using account $user
}

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

# this returns a certificate where the KeyStorageFlags are not set properly to MachineKeySet | PersistKeySet
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

# this requires the certificate to have KeyStorageFlags MachineKeySet | PersistKeySet
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

function Import-Cert {
    param (
        [string] $clientCertFile,
        [string] $storeLocation
    )

    $role = $null
    $cred = Get-Credential -UserName 'Installer' -Message ('Password for ' + $clientCertFile)
    if ($cred) {
        try {
            $clientCert = Import-PfxCertificate -FilePath $clientCertFile -CertStoreLocation $storeLocation -Password $cred.Password
            $subjectName = $clientCert.SubjectName.Decode(128)
            if ($subjectName -match 'OID\.2\.5\.4\.72=([^,]*)') { 
                $role = $Matches[1]
            }
        }
        catch {
            $clientCert = $null
        }
    }

    Write-Output $role, $clientCert
}

function Update-AppSettings {
    param (
        [string] $jsonFile,
        $clientCert
    )

    [PSCustomObject] $jsonObject = Load-JsonObject $jsonFile

    if ($managerUrl -ne '') {
        $jsonObject.Control | Add-Member -Force -MemberType NoteProperty -Name 'Uri' -Value $managerUrl
    }

    if ($clientCert) {
        $subjectCN = $clientCert.GetNameInfo('SimpleName', $false)
        $jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'SubjectCN' -Value $subjectCN
    } else {
        # if no client certificate is supplied, we add the SubjectRole property so that an already installed certificate can be matched
        $jsonObject.Control.ClientCertificate | Add-Member -Force -MemberType NoteProperty -Name 'SubjectRole' -Value 'etw-pushagent'
    }

    Save-JsonObject $jsonFile $jsonObject
}

################ End Functions #################

#Write-Host $targetDir
#Write-Host $managerUrl

$sourceDirPath = [System.IO.Path]::GetFullPath($sourceDir)
$targetDirPath = [System.IO.Path]::GetFullPath($targetDir)

# install root certificate
Write-Host Importing root certificate
$rootCertPath = [System.IO.Path]::Combine($sourceDirPath, "Kd-Soft.cer")
Import-Certificate -FilePath $rootCertPath -CertStoreLocation Cert:\LocalMachine\Root

# process first client certificate matching role=etw-pushagent
Write-Host
Write-Host Checking client certificates
$clientCert = $null
foreach ($clientCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    $role, $clientCert = Import-Cert $clientCertFile cert:\localMachine\my
    if ($role -eq 'etw-pushagent') {
        Write-Host Using client certificate $clientCertFile
        break
    } else {
        $clientCert = $null
    }
}

# if password is empty, create a dummy one to allow credentials for system accounts: 
#NT AUTHORITY\LOCAL SERVICE
#NT AUTHORITY\NETWORK SERVICE
if ($pwd -eq '') {
    $secpwd = (new-object System.Security.SecureString)
}
else {
    $secpwd = ConvertTo-SecureString $pwd -AsPlainText -Force
}
$cred = New-Object System.Management.Automation.PSCredential ($user, $secpwd)

$existingService  = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
if ($existingService) {
  "'$serviceName' exists already. Stopping."
  Stop-Service $serviceName
  "Waiting 6 seconds to allow existing service to stop."
  Start-Sleep -s 6
    
  $existingService.Delete()
  "Waiting 5 seconds to allow service to be uninstalled."
  Start-Sleep -s 5  
}
  
echo "Install Directory: $targetDirPath"

# clean target directory
Remove-Item $targetDirPath -Force -Recurse -ErrorAction SilentlyContinue
if (!(test-path $targetDirPath)) {
  New-Item -ItemType "directory" -Path $targetDirPath
}

# LocalSystem already has all kinds of permissions
if ($cred.UserName -notlike '*\LocalSystem') {
    $acl = Get-Acl "$targetDirPath"
    $aclRuleArgs = $cred.UserName, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($aclRuleArgs)
    #$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($cred.UserName, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule)
    $acl | Set-Acl "$targetDirPath"
}

# copy source publish directory to target directory
$sourcePublishPath = [System.IO.Path]::Combine($sourceDirPath, "publish", "*") 
Copy-Item -Path $sourcePublishPath -Destination $targetDirPath -Recurse

# copy appsettings.Local.json to target directory and update it there
[string] $localAppSettingsSource = '.\appsettings.Local.json'
[string] $localAppSettingsTarget = [System.IO.Path]::Combine($targetDirPath, "appsettings.Local.json")
Copy-Item -Path $localAppSettingsSource -Destination $localAppSettingsTarget
Update-AppSettings $localAppSettingsTarget $clientCert

# path of service binary executable
$filepath = [System.IO.Path]::Combine($targetDirPath, $file)

"Installing the service."
if ($cred.UserName -like 'NT SERVICE\*') {
    # for a virtual account (NT SERVICE\*) we need to pass a null password which PSCredential does not support, so we use $newService.Change()
    New-Service -Name $serviceName -BinaryPathName $filepath -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic
    $newService  = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
    $ChangeStatus = $newService.Change($null, $null, $null, $null, $null, $null, $cred.UserName, $null, $null, $null, $null)
    If ($ChangeStatus.ReturnValue -eq '0')  {
        Write-host Log on account updated sucessfully for the service $newService -f Green
        # for lack of a better understanding of minimum permissions, we use Administrator rights
        net localgroup Administrators /delete $cred.UserName
        net localgroup Administrators /add $cred.UserName
    } Else {
        Write-host Failed to update Log on account in the service $newService. Error code: $($ChangeStatus.ReturnValue) -f Red
    }
} else {
    New-Service -Name $serviceName -BinaryPathName $filepath -Credential $cred -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic
}

"Configuring the service"
sc.exe failure $serviceName reset=86400 actions=restart/6000/restart/6000/restart/6000

"Installed and configured the service."

#$ShouldStartService = Read-Host "Would you like the '$serviceName ' service started? Y or N"
#if($ShouldStartService -eq "Y") {
    "Starting the service."
    Start-Service $serviceName
#}
"Completed."
