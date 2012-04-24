properties { 
  $base_dir  = Split-Path $psake.build_script_file
  $lib_dir = "$base_dir\Lib"
  $build_dir = "$base_dir\build" 
  $buildartifacts_dir = "$build_dir\" 
  $sln_file = "$base_dir\ServiceBroker.Queues.sln" 
  $version = "1.0.0.0"
  $tools_dir = "$base_dir\Tools"
  $release_dir = "$base_dir\Release"
} 

include .\psake_ext.ps1
	
Task Default -depends Release

Task Clean { 
  remove-item -force -recurse $buildartifacts_dir -ErrorAction SilentlyContinue 
  remove-item -force -recurse $release_dir -ErrorAction SilentlyContinue 
} 

Task Init -depends Clean { 
	Generate-Assembly-Info `
		-file "$base_dir\ServiceBroker.Queues\Properties\AssemblyInfo.cs" `
		-title "ServiceBroker Queues $version" `
		-description "SQL Server Service Broker queue API" `
		-company "" `
		-product "ServiceBroker Queues $version" `
		-version $version `
		-copyright "" `
        -clsCompliant "true"
		
	Generate-Assembly-Info `
		-file "$base_dir\ServiceBroker.Queues.Tests\Properties\AssemblyInfo.cs" `
		-title "ServiceBroker Queues $version" `
		-description "SQL Server Service Broker queue API" `
		-company "" `
		-product "ServiceBroker Queues $version" `
		-version $version `
		-copyright "" `
        -clsCompliant "true"
        
	new-item $release_dir -itemType directory 
	new-item $buildartifacts_dir -itemType directory 
} 

Task Compile -depends Init { 
  Exec { msbuild $sln_file /t:Rebuild /p:Configuration=Release /p:OutDir=$buildartifacts_dir/ }
} 

Task Test -depends Compile {
  $old = pwd
  cd $build_dir
  Exec { "$tools_dir\xUnit\xunit.console.exe $build_dir\ServiceBroker.Queues.Tests.dll" }
  cd $old		
}


Task Release -depends Test {
	& $tools_dir\zip.exe -9 -A -j `
		$release_dir\ServiceBroker.Queues.zip `
        $build_dir\ServiceBroker.Queues.dll `
        $build_dir\ServiceBroker.Queues.xml `
		license.txt `
		acknowledgements.txt
	if ($lastExitCode -ne 0) {
        throw "Error: Failed to execute ZIP command"
    }
}