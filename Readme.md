# MonoTrainer
Funny tool for your trainers for unity-mono based windows 64bit games!
# WIP
# Getting Starter
1) SDKGenerator.exe `-f "path/to/Assembly-Csharp.dll" "path/to/other_optional.dll`
 - or path to main game lib. For example "The Last Stand" uses "TheLastStand.dll" instead of "Assembly-Csharp.dll"
 - you can use `-w SomeCoolType SomeSecondCoolType` to create a proxy only for these types. However, this doesn't work super well.
2) Create new .net 6.0 project. Console or App.
3) Add generated GameSDK.dll to your project and add this references:
```
	<ItemGroup>
		<PackageReference Include="Reloaded.Assembler" Version="1.0.14" />
		<PackageReference Include="Reloaded.Injector" Version="1.2.5" />
		<PackageReference Include="Reloaded.Memory" Version="8.1.0" />
	</ItemGroup>
```
	- Reloaded.Memory 9.0.0 and higher is not supported due to huge changes.
4) Init GameSDK:
```csharp
var myGame = Process.GetProcessesByName("gameName")[0];
SDK.MonoBridge.Init(myGame);
```
5) Have fun with GameSDK.* namespace!
# Examples
[The Last Stand Pseudo Trainer for old version](https://github.com/BadRyuner/MonoTrainer/blob/master/TheLastSpellTrainer/Program.cs)
[Ravenfield + Raylib](https://github.com/BadRyuner/MonoTrainer/blob/master/RavenfieldExample/Program.cs)
# Lib can do 100%
- Get/Set non-generic fields and non-generic non-valuetype (!!!) properties.
- Call non-generic methods (with instance and static) with 0-5 args.
- Read/Create mono strings.

# Partially working
- Generics. Generic types (not generic methods!!) with 1 argument are currently supported, only methods.

# Bugs
- Dotnet or Mono sometimes lose pointers xD :D

# Not working at the moment
- And a lot of things, probably.