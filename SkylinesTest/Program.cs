using GameSDK.Game.Game.Simulation;
using GameSDK.Unity_Entities.Unity.Entities;
using SDK;
using System.Diagnostics;

namespace SkylinesTest;

internal class Program
{
	static void Main(string[] args)
	{
		MonoBridge.Init(Process.GetProcessesByName("Cities2")[0]);

		while(true)
		{
			switch (Console.ReadLine())
			{
				case "money":
					{
						var current = World.get_DefaultGameObjectInjectionWorld();
						var citySystem = current.GetExistingSystemManaged<CitySystem>();
						Console.WriteLine(citySystem.m_Money);

						// this.m_Money = World.DefaultGameObjectInjectionWorld
						// .GetExistingSystemManaged<CitySystem>()
						// .EntityManager
						// .GetComponentData<PlayerMoney>(this.m_City)
						// .money;
					}
					break;
				case "addmoney":
					{
						var current = World.get_DefaultGameObjectInjectionWorld();
						var system = current.GetExistingSystemManaged(new GameSDK.mscorlib.System.Type(CitySystem.TypeOf()));
						var citysys = new CitySystem(system._this);
						var cfg = citysys.m_CityConfigurationSystem;
						cfg.m_UnlimitedMoney = true;
					}
					break;

				case "exit":
					return;
			}
		}
	}
}
