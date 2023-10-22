using CommandLine;
using System.IO;

namespace SDKGenerator;

internal unsafe class Program
{
	static void Main(string[] args)
	{
#if true
		args = new string[] { "-f",
			"D:/Games/Endless Space 2 Dark Matter/EndlessSpace2_Data/Managed/Assembly-CSharp.dll",
			"-w",
			"AcademyCouncil",
			"Amplitude.StaticString",
			"DepartmentOfCommerce"};
#endif
		Parser.Default.ParseArguments<Arguments>(args)
			.WithParsed((res) =>
			{
				string dll = res.TargetDlls.First();
				if (dll[0] == ' ') // nice joke, CommandLine.
					dll = dll.Remove(0, 1);

				string save = Path.Combine(new FileInfo(dll).Directory.FullName, "SDK");
				new Generator(dll, res).GenerateSDK(save);
				Console.WriteLine("TRAINER TRAINER MONO WINNER!");
			})
			.WithNotParsed((err) =>
			{
				foreach (var er in err)
				{
					Console.WriteLine(er);
				}
				Console.WriteLine("\nPress any key...");
				Console.ReadKey();
			});

		
		return;
	}
}

public class Arguments
{
	[Option("get-everything-from-mscorlib", Default = false, Required = false, HelpText = "Generates proxy types for each class in mscorlib, if even they are not used.")]
	public bool GetEverythingFromMscorlib { get; set; }

	[Option("force-unitycore", Default = false, Required = false, HelpText = "Generates proxy types for each class in UnityEngine.CoreModule, if even they are not used.")]
	public bool GetEverythingFromUnityCoreModule { get; set; }

	[Option('f', "files", Default = null, Required = true, HelpText = "List of dlls (or one dll) for proxy generation")]
	public IEnumerable<string> TargetDlls { get; set; }

	[Option('w', "whitelist", Default = null, Required = false, HelpText = "Generate proxies only for these types with minimal use of dependencies. Significantly reduces weight.")]
	public IEnumerable<string> ClassWhitelist { get; set; }
}