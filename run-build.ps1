remove-module [p]sake
&.\Tools\Nuget install psake -version 4.4.1
Import-Module .\packages\psake.4.4.1\tools\psake.psm1
invoke-psake .\default.ps1
if ($psake.build_success -eq $false) { exit 1 } else { exit 0 }