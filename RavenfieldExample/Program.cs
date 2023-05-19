using GameSDK.Assembly_CSharp;
using Raylib_cs;
using SDK;
using System.Diagnostics;

nuint lastexc = 0;

MonoBridge.Init(Process.GetProcessesByName("ravenfield")[0]); // you can catch there exceptions

var height = GameSDK.UnityEngine_CoreModule.UnityEngine.Screen.get_height();
var width = GameSDK.UnityEngine_CoreModule.UnityEngine.Screen.get_width();

Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_TRANSPARENT | ConfigFlags.FLAG_VSYNC_HINT | ConfigFlags.FLAG_WINDOW_MOUSE_PASSTHROUGH | ConfigFlags.FLAG_WINDOW_TOPMOST); // 
Raylib.InitWindow(width, height, "Aboba");
//Raylib.SetTargetFPS(60);

while (!Raylib.WindowShouldClose())
{
	Raylib.BeginDrawing();
	Raylib.ClearBackground(Color.BLANK);

	var instance = ActorManager.instance;
	var camera = GameSDK.UnityEngine_CoreModule.UnityEngine.Camera.get_main();
	if (IsAlive(instance._this) && IsAlive(camera._this))
	{
		var player = instance.player;
		if (IsAlive(player._this))
		{
			var myteam = player.GetBase().team;
			var actors = ActorManager.AliveActorsOnTeam(myteam == 1 ? 0 : 1); // dont use general lists, sometimes they can show you GAME CRASHED due LIST HAS BEEN CHANGED, AAAAAAAAAGRH
			if ((ulong)actors._this < 100) goto render; // skip :(
			var count = actors.get_Count();
			for (int i = 0; i < count; i++) // this awful *_*
			{
				var actor = actors.get_Item(i); // VeeeeeeeeeeeeeeeRY slow, because VIRTUAL
				if (!IsAlive(actor._this)) continue;
				var transform = actor.GetBase().GetBase().GetBase().GetBase().get_transform(); // this also awful
				if (!IsAlive(transform._this)) continue;
				var screen = camera.WorldToScreenPoint(transform.get_position()); // but it works

				if (screen.z > 1)
					Raylib.DrawCircle((int)screen.x, height - (int)screen.y, 3f, Color.MAGENTA);
			}
		}
	}

	render:
	Raylib.EndDrawing();
}

MonoBridge.Free();
Raylib.CloseWindow();

void CatchExc() // universal thing
{
	var exc = MonoBridge.refException.GetValue();
	if (exc != 0 || exc != lastexc)
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

static bool IsAlive(UIntPtr test) // for Unity Objects only
{
	if (test == UIntPtr.Zero) return false;
	return GameSDK.UnityEngine_CoreModule.UnityEngine.Object.IsNativeObjectAlive(MonoBridge.Cast<GameSDK.UnityEngine_CoreModule.UnityEngine.Object>(test));
}