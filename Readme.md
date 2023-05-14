# MonoTrainer
Funny tool for your trainers for unity-mono based windows games!
# WIP
# Getting Starter
1) .\SDKGenerator.exe "path/to/Assembly-Csharp.dll"
1.1) or path to main game lib. For example "The Last Stand" uses "TheLastStand.dll" instead of "Assembly-Csharp.dll"
2) Create new .net 6.0 project. Console or App.
3) Add generated GameSDK.dll to your project and add this references:
```
    <ItemGroup>
		<PackageReference Include="Reloaded.Assembler" Version="1.0.14" />
		<PackageReference Include="Reloaded.Injector" Version="1.2.5" />
		<PackageReference Include="Reloaded.Memory" Version="8.1.0" />
	</ItemGroup>
```
4) Init GameSDK:
```csharp
var myGame = Process.GetProcessesByName("gameName")[0];
SDK.MonoBridge.Init(myGame);
```
5) Do your funny things!
# Lib can do 100%
- Get/Set non-generic fields and non-generic non-valuetype (!!!) properties.
- Call non-generic methods (with instance and static) with 0-5 args.

# Not working at the moment
- String things. You can only get first char and length, hahah. Maybe in the future it will be possible to alloc strings and read/write them.
- Generic things. Very scary thing. There is little information on Google about interacting with Generic via mono native api. This causes problems with games that use Singleton<Aboba>.Instance instead of Aboba.Instance.
- And a lot of things, probably.