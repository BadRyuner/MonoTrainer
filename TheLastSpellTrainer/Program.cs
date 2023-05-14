using System.Diagnostics;

Console.WriteLine("Hello, bro!");

TryFindGame:
var good = true;
try
{
	SDK.MonoBridge.Init(Process.GetProcessesByName("The Last Spell")[0]);
}
catch(Exception)
{
	Console.WriteLine("Cant find game...\nPress any button to try again.");
	good = false;
}
if (!good) goto TryFindGame;

Console.WriteLine("Aviable commands:");
Console.WriteLine("souls - adds 5k damned souls");
Console.WriteLine("exit - stop trainer and free shellcode resources");

while (true)
{
	switch (Console.ReadLine())
	{
		case "souls":
			// GameSDK - Lib NameSpace
			// .TheLastStand - dll Name
			// .TheLastStand.Manager - namespace
			// .ApplicationManager - class
			// .get_Application - static property
			var app = GameSDK.TheLastStand.TheLastStand.Manager.ApplicationManager.get_Application();
			// .damnedSouls = field proxy
			app.damnedSouls += 5000;
			break;
		case "exit":
			// free shellcode and resources
			SDK.MonoBridge.Free();
			return;
		default: break;
	}
}