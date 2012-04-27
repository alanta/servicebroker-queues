properties { 
  if ($build_number -eq $null) {
    $build_number = 0 
  }
  $base_dir  = Split-Path $psake.build_script_file
  $lib_dir = "$base_dir\Lib"
  $build_dir = "$base_dir\build" 
  $buildartifacts_dir = "$build_dir\artifacts"
  $sln_file = "$base_dir\ServiceBroker.Queues.sln" 
  $version = "1.1.1"
  $assembly_version = "$version.$build_number"
  $tools_dir = "$base_dir\Tools"
  $release_dir = "$base_dir\Release"
  $xunit = "$base_dir\Tools\xUnit\xunit.console.clr4.exe"
} 

include .\psake_ext.ps1
	
Task Default -depends Release

Task Clean { 
  remove-item -force -recurse $build_dir -ErrorAction SilentlyContinue
  remove-item -force -recurse $release_dir -ErrorAction SilentlyContinue
} 

Task Init -depends Clean { 
	Generate-Assembly-Info `
		-file "$base_dir\ServiceBroker.Queues\Properties\AssemblyInfo.cs" `
		-title "ServiceBroker Queues $version" `
		-description "SQL Server Service Broker queue API" `
		-company "" `
		-product "ServiceBroker Queues $version" `
		-version $assembly_version `
		-copyright "" `
        -clsCompliant "true"

	Generate-Assembly-Info `
		-file "$base_dir\ServiceBroker.Queues.Tests\Properties\AssemblyInfo.cs" `
		-title "ServiceBroker Queues $version" `
		-description "SQL Server Service Broker queue API" `
		-company "" `
		-product "ServiceBroker Queues $version" `
		-version $assembly_version `
		-copyright "" `
        -clsCompliant "true"
        
    Generate-Assembly-Info `
		-file "$base_dir\ServiceBroker.Install\Properties\AssemblyInfo.cs" `
		-title "ServiceBroker Queues Installer $version" `
		-description "SQL Server Service Broker queue Installer" `
		-company "" `
		-product "ServiceBroker Queues $version" `
		-version $assembly_version `
		-copyright "" `
        -clsCompliant "true"

	new-item $release_dir -itemType directory | Out-Null
    new-item $build_dir -itemType directory | Out-Null
	new-item $buildartifacts_dir -itemType directory  | Out-Null
} 

Task Compile -depends Init { 
  Exec { msbuild $sln_file /t:Rebuild /p:Configuration=Release /p:OutDir=$buildartifacts_dir/ }
} 

Task Test -depends Compile {
  Exec { .$xunit $buildartifacts_dir\ServiceBroker.Queues.Tests.dll }
}

Task Release -depends Test {
	& $tools_dir\zip.exe -9 -A -j `
		$release_dir\ServiceBroker.Queues.zip `
      $buildartifacts_dir\ServiceBroker.Queues.dll `
      $buildartifacts_dir\ServiceBroker.Queues.xml `
      $buildartifacts_dir\ServiceBroker.Install.exe `
      $buildartifacts_dir\DbUp.dll `
      $buildartifacts_dir\Common.Logging.dll `
		  license.txt `
		acknowledgements.txt
	if ($lastExitCode -ne 0) {
        throw "Error: Failed to execute ZIP command"
    }

    $nuget_dir = "$build_dir\NuGet"

    new-item $nuget_dir -itemType directory  | Out-Null
    new-item $nuget_dir\lib -itemType directory  | Out-Null
    new-item $nuget_dir\lib\net35 -itemType directory  | Out-Null
    new-item $nuget_dir\tools -itemType directory  | Out-Null

    Copy-Item license.txt $nuget_dir
    Copy-Item acknowledgements.txt $nuget_dir
    Copy-Item $buildartifacts_dir\ServiceBroker.Queues.dll $nuget_dir\lib\net35
    Copy-Item $buildartifacts_dir\ServiceBroker.Queues.xml $nuget_dir\lib\net35
    Copy-Item $buildartifacts_dir\ServiceBroker.Queues.xml $nuget_dir\tools

    Copy-Item *.nuspec $nuget_dir

    $packages = Get-ChildItem $nuget_dir *.nuspec -recurse
	$packages | ForEach-Object {
		$nuspec = [xml](Get-Content $_.FullName)
		$nuspec.package.metadata.version = $version

		$nuspec.Save($_.FullName);
		&"$tools_dir\nuget.exe" pack $_.FullName -OutputDirectory $release_dir
	}
}