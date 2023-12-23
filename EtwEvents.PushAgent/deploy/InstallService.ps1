#Requires -Version 5.1

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

    $jsonObject | ConvertTo-Json -Compress -Depth 10 | Set-Content -Path $jsonFilePath
}

function Merge-Json($source, $extend) {
    if ($source -is [PSCustomObject] -and $extend -is [PSCustomObject]) {
        # Ordered hashtable for collecting properties
        $merged = [ordered] @{}

        # Copy $source properties or overwrite by $extend properties recursively
        foreach ($property in $source.PSObject.Properties) {
            if ($null -eq $extend.$($property.Name)) {
                $merged[$property.Name] = $property.Value
            }
            else {
                $merged[$property.Name] = Merge-Json $property.Value $extend.$($property.Name)
            }
        }

        # Add $extend properties
        foreach ($property in $extend.PSObject.Properties) {
            if ($null -eq $source.$($property.Name)) {
                $merged[$property.Name] = $property.Value
            }
        }

        # Convert hashtable into PSCustomObject and output
        [PSCustomObject] $merged
    }
    elseif ($source -is [Collections.IList] -and $extend -is [Collections.IList]) {
        $maxCount = [Math]::Max($source.Count, $extend.Count)

        [array] $merged = for ($i = 0; $i -lt $maxCount; ++$i) {
            if ($i -ge $source.Count) { 
                # extend array is bigger than source array
                $extend[$i]
            }              
            elseif ($i -ge $extend.Count) {
                # source array is bigger than extend array
                $source[$i]
            }
            else {
                # Merge the elements recursively
                Merge-Json $source[$i] $extend[$i]
            }
        }

        # Output merged array, using comma operator to prevent enumeration 
        , $merged
    }
    else {
        # Output extend object (scalar or different types)
        $extend
    }
}

# this returns a certificate where the KeyStorageFlags are not set properly to MachineKeySet | PersistKeySet
function Get-CertPfxItems {
    param (
        [string] $certFilePath
    )

    try {
        $clientCert = Get-PfxCertificate -FilePath $certFilePath
    }
    catch {
        $clientCert = $null
    }

    if ($clientCert) {
        $subjectName = $clientCert.SubjectName.Decode(128)
        if ($subjectName -match 'OID\.2\.5\.4\.72=([^,]*)') { 
            $role = $Matches[1]
        }
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
    $certCred = Get-Credential -UserName 'Installer' -Message ('Password for ' + $clientCertFile)
    if ($certCred) {
        try {
            $clientCert = Import-PfxCertificate -FilePath $clientCertFile -CertStoreLocation $storeLocation -Password $certCred.Password
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

# produces two files: 1) target file merged into source file, 2) backup of target file
function Merge-AppSettings {
    param (
        [string]$jsonFile,
        [string]$targetDir
    )

    [string] $sourceFile = '.\' + $jsonFile
    [string] $targetFile = [System.IO.Path]::Combine($targetDir, $jsonFile)
    [string] $localTargetFileMerged = $sourceFile + '.merged'

    if (Test-Path -Path $targetFile -PathType leaf) {
        [string] $localTargetFileBackup = $sourceFile + '.bkp'
        Copy-Item -Path $targetFile -Destination $localTargetFileBackup

        [PSCustomObject] $targetObj = Load-JsonObject $localTargetFileBackup
        [PSCustomObject] $sourceObj = Load-JsonObject $sourceFile
        $mergedObj = Merge-Json $sourceObj $targetObj

        Save-JsonObject $localTargetFileMerged $mergedObj
    }
    else {
        Copy-Item -Path $sourceFile -Destination $localTargetFileMerged
    }
}

function Copy-AppSettings {
    param (
        [string]$jsonFile,
        [string]$targetDir
    )

    [string] $sourceFile = '.\' + $jsonFile

    [string] $localTargetFileMerged = $sourceFile + '.merged'
    if (Test-Path -Path $localTargetFileMerged -PathType leaf) {
        [string] $targetFile = [System.IO.Path]::Combine($targetDir, $jsonFile)
        Copy-Item -Path $localTargetFileMerged -Destination $targetFile
    }

    [string] $localTargetFileBackup = $sourceFile + '.bkp'
    if (Test-Path -Path $localTargetFileBackup -PathType leaf) {
        [string] $targetFileBackup = [System.IO.Path]::Combine($targetDir, $jsonFile) + '.bkp'
        Copy-Item -Path $localTargetFileBackup -Destination $targetFileBackup
    }
}

function Update-MergedAppSettings {
    param (
        $clientCert,
        [String]$mgrUrl
    )

    [string] $jsonFile = '.\appsettings.Local.json.merged'
    [PSCustomObject] $jsonObject = Load-JsonObject $jsonFile

    if ($mgrUrl -ne '') {
        $jsonObject.Control | Add-Member -Force -MemberType NoteProperty -Name 'Uri' -Value $mgrUrl
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

# install root/signing certificates (chain)
Write-Host Importing root certificate chain
foreach ($caFile in Get-ChildItem -Path $sourceDirPath -Filter '*.cer') {
    Import-Certificate -FilePath $caFile.FullName -CertStoreLocation Cert:\LocalMachine\Root
}

# process any PKCS12 certificates
Write-Host
Write-Host Checking PKCS12 certificates
$clientCert = $null
foreach ($clientCertFile in Get-ChildItem -Path . -Filter '*.p12') {
    $role, $clientCert = Import-Cert $clientCertFile Cert:\localMachine\my
    if ($role -eq 'etw-pushagent') {
        Write-Host Imported certificate $clientCertFile with role 'etw-pushagent'
    } elseif ($clientCert) {
        Write-Host Imported certificate $clientCertFile
    } else {
         Write-Host Not a client certificate: $clientCertFile
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

# merge target settings into source settings to preserve them
Merge-AppSettings 'appsettings.Local.json' $targetDirPath

# backup eventSession.json and eventSinks.Json
[string] $targetEventSession = [System.IO.Path]::Combine($targetDir, 'eventSession.json')
Copy-Item -Path $targetEventSession -Destination '.\eventSession.json' -ErrorAction SilentlyContinue
[string] $targetEventSinks = [System.IO.Path]::Combine($targetDir, 'eventSinks.json')
Copy-Item -Path $targetEventSinks -Destination '.\eventSinks.json' -ErrorAction SilentlyContinue

# clean target directory
Remove-Item $targetDirPath -Force -Recurse -ErrorAction SilentlyContinue
if (!(Test-Path $targetDirPath)) {
    New-Item -ItemType "directory" -Path $targetDirPath
}

# LocalSystem account has all kinds of permissions already
if ($cred.UserName -notlike '*\LocalSystem') {
    $acl = Get-Acl "$targetDirPath"
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($cred.UserName, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule)
    $acl | Set-Acl "$targetDirPath"
}

# copy source publish directory to target directory
$sourcePublishPath = [System.IO.Path]::Combine($sourceDirPath, "publish", "*") 
Copy-Item -Path $sourcePublishPath -Destination $targetDirPath -Recurse

# update appsettings.Local.json.merged with client certificate and managerUrl
Update-MergedAppSettings $clientCert $managerUrl

# now copy merged and updated settings to target directory
Copy-AppSettings 'appsettings.Local.json' $targetDirPath

# restore eventSession.json and eventSinks.Json
Copy-Item -Path '.\eventSession.json' -Destination $targetEventSession -ErrorAction SilentlyContinue
Copy-Item -Path '.\eventSinks.json' -Destination $targetEventSinks -ErrorAction SilentlyContinue

# path of service binary executable
$filepath = [System.IO.Path]::Combine($targetDirPath, $file)

"Installing the service."
if ($cred.UserName -like 'NT SERVICE\*') {
    # for a virtual account (NT SERVICE\*) we need to pass a null password which PSCredential does not support, so we use $newService.Change()
    New-Service -Name $serviceName -BinaryPathName $filepath -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic
    $newService  = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
    $ChangeStatus = $newService.Change($null, $null, $null, $null, $null, $null, $cred.UserName, $null, $null, $null, $null)
    if ($ChangeStatus.ReturnValue -eq '0')  {
        Write-host Log on account updated sucessfully for the service $newService -f Green
        # for lack of a better understanding of minimum permissions, we use Administrator rights
        net localgroup Administrators /delete $cred.UserName
        net localgroup Administrators /add $cred.UserName
    } else {
        Write-host Failed to update Log on account in the service $newService. Error code: $($ChangeStatus.ReturnValue) -f Red
    }
} else {
    New-Service -Name $serviceName -BinaryPathName $filepath -Credential $cred -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic
}

"Configuring the service"
sc.exe failure $serviceName reset= 86400 actions= restart/6000/restart/6000/restart/6000

"Installed and configured the service."

#$ShouldStartService = Read-Host "Would you like the '$serviceName ' service started? Y or N"
#if($ShouldStartService -eq "Y") {
    "Starting the service."
    Start-Service $serviceName
#}
"Completed."
