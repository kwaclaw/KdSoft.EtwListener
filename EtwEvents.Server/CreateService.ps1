param(
    [Parameter(mandatory=$true)][String]$file,
    [Parameter(mandatory=$true)][String]$user,
    [String]$pwd
) 

$serviceName = "EtwListener"
$serviceDescription = "Forwards ETW Events"
$serviceDisplayName = "Etw Listener"

$filepath = [System.IO.Path]::GetFullPath($file)
$dirpath = [System.IO.Path]::GetDirectoryName($filepath)


# if password is empty, create a dummy one to allow have credentials for system accounts: 
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
    $acl = Get-Acl "$dirpath"
    #$aclRuleArgs = $cred, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($user, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule)
    $acl | Set-Acl "$dirpath"
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

"Installing the service."
New-Service -Name $serviceName -BinaryPathName $filepath -Credential $cred -Description $serviceDescription -DisplayName $serviceDisplayName -StartupType Automatic 
"Installed the service."

#$ShouldStartService = Read-Host "Would you like the '$serviceName ' service started? Y or N"
#if($ShouldStartService -eq "Y") {
    "Starting the service."
    Start-Service $serviceName
#}
"Completed."
