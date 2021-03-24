param(
    [Parameter(mandatory=$true)][String]$sourceDir,
    [Parameter(mandatory=$true)][String]$targetDir,
    [Parameter(mandatory=$true)][String]$file,
    [Parameter(mandatory=$true)][String]$user,
    [String]$pwd
) 

$serviceName = "EtwPushAgent"
$serviceDescription = "Forwards ETW Events to Event Sink"
$serviceDisplayName = "Etw PushAgent"

Write-Host $targetDir

$sourceDirPath = [System.IO.Path]::GetFullPath($sourceDir)
$targetDirPath = [System.IO.Path]::GetFullPath($targetDir)

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


#LocalSystem already has all kinds of permissions
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
  #make sure target directory exists
  New-Item -ItemType "directory" -Path $targetDirPath
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
