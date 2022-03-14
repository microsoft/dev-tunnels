echo 'Start host'

$Env:ASPNETCORE_ENVIRONMENT = "Development"
echo 'Settings run location'
Set-Location -Path '../../../../../../../bin/debug/TunnelService.ControlPlane/'

echo 'Copy Settings'
copy -Recurse -Force '../../../src/Settings/' './../../Settings/'

try {
    echo 'Run control plane service'
    dotnet Microsoft.VsSaaS.Services.TunnelService.ControlPlane.dll
}
catch [Exception] {
    echo $_.Exception.GetType().FullName, $_.Exception.Message
}


