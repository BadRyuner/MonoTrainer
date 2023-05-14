using System.Diagnostics;

namespace SDKGenerator;

internal unsafe class Program
{
	static void Main(string[] args)
	{
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