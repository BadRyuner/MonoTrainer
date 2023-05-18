using System.Diagnostics;

namespace SDKGenerator;

internal unsafe class Program
{
	static void Main(string[] args)
	{
#if true
		args = new string[] { "D:\\Games\\The Last Spell\\The Last Spell_Data\\Managed\\TheLastStand.dll" };
		// D:\Steam\steamapps\common\PULSARLostColony\PULSAR_LostColony_Data\Managed\LevelScanner.dll
		// D:\\Games\\The Last Spell\\The Last Spell_Data\\Managed\\TheLastStand.dll
#endif

		if (args.Length == 0)
		{
			Console.WriteLine("Wrong args!");
			return;
		}
		string dll = args[0];
		string save = Path.Combine(new FileInfo(dll).Directory.FullName, "SDK");

		new Generator(dll).GenerateSDK(save);
		Console.WriteLine("TRAINER TRAINER MONO WINNER!");
		return;
	}
}