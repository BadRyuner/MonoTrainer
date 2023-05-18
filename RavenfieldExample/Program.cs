using GameSDK.UnityEngine_CoreModule.UnityEngine.PlayerLoop;
using Raylib_cs;
using System.Diagnostics;

nuint lastexc = 0;

SDK.MonoBridge.Init(Process.GetProcessesByName("ravenfield")[0]); // you can catch there exceptions

Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_TRANSPARENT | ConfigFlags.FLAG_WINDOW_MOUSE_PASSTHROUGH | ConfigFlags.FLAG_WINDOW_TOPMOST); // 
Raylib.InitWindow(1600, 900, "Test"); // idk how to get screen size
Raylib.SetTargetFPS(30);

var height = 900; // GameSDK.UnityEngine_CoreModule.UnityEngine.Screen.get_height()

while (!Raylib.WindowShouldClose())
{
	Raylib.BeginDrawing();
	Raylib.ClearBackground(Color.BLANK);

	var instance = GameSDK.Assembly_CSharp.ActorManager.instance;
	var camera = GameSDK.UnityEngine_CoreModule.UnityEngine.Camera.get_main();
	if ((ulong)instance._this > 100 && (ulong)camera._this > 100)
	{
		var player = instance.player;
		if ((ulong)player._this > 100)
		{
			var myteam = player.GetBase().team;
			var actors = GameSDK.Assembly_CSharp.ActorManager.AliveActorsOnTeam(myteam == 1 ? 0 : 1); // dont use general lists, sometimes they can show you GAME CRASHED, AAAAAAAAAGRH
			CatchExc();
			if ((ulong)actors._this < 100) goto render;
			var count = actors.get_Count();
			for (int i = 0; i < count; i++) // this awful *_*
			{
				CatchExc();
				var actor = actors.get_Item(i);
				if ((ulong)actor._this < 100) continue;
				var transform = actor.GetBase().GetBase().GetBase().GetBase().get_transform(); // this also awful
				if ((ulong)transform._this < 100) continue;
				var screen = camera.WorldToScreenPoint(transform.get_position()); // but this works

				if (screen.z > 1)
					Raylib.DrawCircle((int)screen.x, height - (int)screen.y, 3f, Color.MAGENTA);
			}
		}
	}

	render:
	Raylib.EndDrawing();
}

SDK.MonoBridge.Free();
Raylib.CloseWindow();

void CatchExc() // universal thing
{
	var exc = SDK.MonoBridge.refException.GetValue();
	if (exc != 0 || exc == lastexc)
	{
		var excep = new GameSDK.mscorlib.System.Exception(exc);
		var where = excep.GetStackTrace(false);
		var mes = excep._message;
		if (mes._this != UIntPtr.Zero)
			Console.WriteLine(mes.GetString());
		if (where._this != UIntPtr.Zero)
			Console.WriteLine(where.GetString());
		lastexc = exc;
	}
}
