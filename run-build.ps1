remove-module [p]sake
Import-Module .\packages\psake.4.1.0\tools\psake.psm1
invoke-psake .\default.ps1
if ($psake.build_success -eq $false) { exit 1 } else { exit 0 }