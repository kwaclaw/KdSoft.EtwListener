param(
    [Parameter(mandatory=$true)][String]$sourceDir,
    [Parameter(mandatory=$true)][String]$targetDir,
    [Parameter(mandatory=$true)][String]$file,
    [String]$serviceName = 'EtwAgentManager',
    [String]$serviceDisplayName = 'Etw Agent Manager',
    [String]$serviceDescription = 'Manages ETW Push Agents',
    [String]$user = '',
    [String]$pwd = '',
    [int]$port = 0
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

function Import-Cert {
    param (
        [string] $serverCertFile,
        [string] $storeLocation
    )

    $cred = Get-Credential -UserName 'Installer' -Message ('Password for ' + $serverCertFile)
    if ($cred) {
        try {
            $serverCert = Import-PfxCertificate -FilePath $serverCertFile -CertStoreLocation $storeLocation -Password $cred.Password
        }
        catch {
            $serverCert = $null
        }
    }

    Write-Output $serverCert
}

function Update-AppSettings {
    param (
        [string]$jsonFile,
        $serverCert
    )

    [PSCustomObject] $jsonObject = Load-JsonObject $jsonFile
    $endPointsObject = $jsonObject.Kestrel.Endpoints
    $httpsObject = $endPointsObject.Https
    
    # need to move our endpoints to unoccupied ports
    if ($port -ne 0) {
        $httpsUrl = 'https://0.0.0.0:' + $port
        $httpsObject | Add-Member -Force -MemberType NoteProperty -Name 'Url' -Value $httpsUrl
        $httpUrl = 'http://0.0.0.0:' + ($port + 1)
        $endPointsObject.Http | Add-Member -Force -MemberType NoteProperty -Name 'Url' -Value $httpUrl
    }
    
    $certObject = $httpsObject.Certificate
    if ($certObject) {
        #$certObject.psobject.properties.remove('Path')
        #$certObject.psobject.properties.remove('Password')
        # we can't remove already loaded properties, so we must override them and hope it works
    } else {
        $newObj = [PSCustomObject]@{}
        $httpsObject | Add-Member -Force -MemberType NoteProperty -Name 'Certificate' -Value $newObj
        $certObject = $httpsObject.Certificate
    }
    $certObject | Add-Member -Force -MemberType NoteProperty -Name 'Path' -Value $null
    $certObject | Add-Member -Force -MemberType NoteProperty -Name 'Password' -Value $null
    $certObject | Add-Member -Force -MemberType NoteProperty -Name 'Store' -Value 'My'
    $certObject | Add-Member -Force -MemberType NoteProperty -Name 'Location' -Value 'LocalMachine'
    
    $dnsName = $serverCert.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
    $certObject | Add-Member -Force -MemberType NoteProperty -Name 'Subject' -Value $dnsName
    
    Save-JsonObject $jsonFile $jsonObject
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
$serverCert = $null
foreach ($serverCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    $serverCert = Import-Cert $serverCertFile cert:\LocalMachine\My
    if ($serverCert) {
        Write-Host Using server certificate $serverCertFile
        break
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
Update-AppSettings $localAppSettingsTarget $serverCert

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
