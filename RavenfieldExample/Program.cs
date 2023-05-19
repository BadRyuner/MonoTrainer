using GameSDK.Assembly_CSharp;
using GameSDK.UnityEngine_CoreModule.UnityEngine;
using Raylib_cs;
using SDK;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Color = Raylib_cs.Color;

MonoBridge.Init(Process.GetProcessesByName("ravenfield")[0]); // you can catch there exceptions

var height = Screen.get_height();
var width = Screen.get_width();

Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_TRANSPARENT | ConfigFlags.FLAG_VSYNC_HINT | ConfigFlags.FLAG_WINDOW_MOUSE_PASSTHROUGH | ConfigFlags.FLAG_WINDOW_TOPMOST);
Raylib.InitWindow(width, height, "Aboba");
Raylib.SetTargetFPS(60);

GameSDK.mscorlib.System.Collections.Generic.List<Actor> actors = default;
int team = 3;

while (!Raylib.WindowShouldClose())
{
	Raylib.BeginDrawing();
	Raylib.ClearBackground(Color.BLANK);

	var instance = ActorManager.instance;
	var camera = Camera.get_main();
	if (IsAlive(camera._this) && IsAlive(instance._this))
	{
		var player = instance.player;
		if (IsAlive(player._this))
		{
			var myteam = Unsafe.As<Actor, Hurtable>(ref player).team;
			if ((ulong)actors._this < 100 || team != myteam)
			{
				team = myteam;
				actors = ActorManager.AliveActorsOnTeam(myteam == 1 ? 0 : 1); // dont use general lists, sometimes they can show you GAME CRASHED due LIST HAS BEEN CHANGED, AAAAAAAAAGRH
				if ((ulong)actors._this < 100) goto render; // skip :(
			}
			var count = actors.get_Count();
			for (int i = 0; i < count; i++) // this awful *_*
			{
				var actor = actors.get_Item(i); // VeeeeeeeeeeeeeeeRY slow, because VIRTUAL
				if (!IsAlive(actor._this)) continue;
				var transform = actor.GetBase().GetBase().GetBase().GetBase().get_transform(); // this also awful
				if (!IsAlive(transform._this)) continue;
				var screen = camera.WorldToScreenPoint(transform.get_position()); // but it works

				// draw just point
				if (screen.z > 1)
					Raylib.DrawCircle((int)screen.x, height - (int)screen.y, 3f, Color.MAGENTA);

				// or

				/*
				// draw line
				if (screen.z > 1)
					Raylib.DrawLine((int)screen.x, height - (int)screen.y, width/2, height, Color.MAGENTA);
				*/
			}
		}
	}
	else 
		actors = default;

	render:
	Raylib.EndDrawing();
	GameSDK.mscorlib.System.GC.Collect();
	GC.Collect();
}

Raylib.CloseWindow();
MonoBridge.Free();

static bool IsAlive(UIntPtr test) // for Unity Objects only
{
	if (test == UIntPtr.Zero) return false;
	return GameSDK.UnityEngine_CoreModule.UnityEngine.Object.IsNativeObjectAlive(MonoBridge.Cast<GameSDK.UnityEngine_CoreModule.UnityEngine.Object>(test));
}