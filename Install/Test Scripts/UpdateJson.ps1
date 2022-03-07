[string] $appSettingsFile = 'appSettings.Local.json'
[string] $content = Get-Content -Raw -Path $appSettingsFile
[PSCustomObject] $jsonObject = $content | ConvertFrom-Json

$subjectRole = 'etw-pushagent'
$jsonObject.Control.ClientCertificate.SubjectRole = $subjectRole
$jsonObject | ConvertTo-Json -Compress -Depth 6 | Set-Content $appSettingsFile