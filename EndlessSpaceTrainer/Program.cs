using GameSDK;
using GameSDK.Assembly_CSharp;
using SDK;
using System.Diagnostics;

Console.WriteLine("Welcome to Endless Space 2 Trainer!");
Console.WriteLine("Commands:\n Dust100 - adds 100 dust 2 u");

MonoBridge.Init(Process.GetProcessesByName("EndlessSpace2")[0]);

while (true)
{
	switch(Console.ReadLine())
	{
		case "Dust100":
			var game = Game.GetGame();
			var empire = game.get_AcademyEmpire();
			Console.WriteLine(empire.GetBase().GetMasterOfDustMoney());
			var agencies = game.get_AcademyEmpire().GetBase().agenciesArray;
			var length = agencies.get_Length();
			for(int i = 0; i < length; i++)
			{
				var agencyObj = agencies.GetValue(i);
				var agency = new Agency(agencyObj._this);
				var agencyType = agency.GetBase().GetBase().GetType();
				var result = agencyType.get_AssemblyQualifiedName().GetString() == DepartmentOfTheTreasury.aqn(); // works like if (SomeObject is DepartmentOfTheTreasury)
				if (result)
				{
					var dof = new DepartmentOfTheTreasury(agency._this); // cast to DoF

					// and oops, instance generic methods in non-generic types and out arguments are still not supported.
					// Maybe there is another way to change the currency value, but I don't know it.

					break;
				}
			}
			break;
		case "exit":
			MonoBridge.Free();
			Environment.Exit(0);
			break;
	}
}