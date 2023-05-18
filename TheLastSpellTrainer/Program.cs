using SDK;
using System.Diagnostics;

Console.WriteLine("Hello, bro!");

TryFindGame:
var good = true;
try
{
	SDK.MonoBridge.Init(Process.GetProcessesByName("The Last Spell")[0]); //The Last Spell
}
catch(Exception)
{
	Console.WriteLine("Cant find game...\nPress any button to try again.");
	good = false;
}
if (!good) goto TryFindGame;

Console.WriteLine("Aviable commands:");
Console.WriteLine("souls - adds 5k damned souls");
Console.WriteLine("gold - adds 100 gold");
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
			// app.damnedSouls += 5000;
			// or via property proxy to trigger game events
			var souls = app.get_DamnedSouls(); // looks awful, but im lazy to add property support :(
			app.set_DamnedSouls(souls + 5000);
			break;
		case "gold":
			var res = GameSDK.TPLib.TPLib.TPSingleton<GameSDK.TheLastStand.TheLastStand.Manager.ResourceManager>.get_Instance();
			var gold = res.get_Gold();
			res.set_Gold(gold + 100);

			break;
		case "exit":
			// free shellcode and resources
			SDK.MonoBridge.Free();
			return;
		default: break;
	}
}

static void CatchExc() // universal thing
{
	var exc = SDK.MonoBridge.refException.GetValue();
	if (exc != 0)
	{
		var excep = new GameSDK.mscorlib.System.Exception(exc);
		var where = excep.GetStackTrace(false);
		var mes = excep._message;
		if (mes._this != UIntPtr.Zero)
			Console.WriteLine(mes.GetString());
		if (where._this != UIntPtr.Zero)
			Console.WriteLine(where.GetString());
	}
}