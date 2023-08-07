using Leopotam.EcsLite;
using UnityEngine;

namespace Nashet.ECS
{
	public class ECSRunner : MonoBehaviour
	{
		public bool IsReady { get; private set; }

		public EcsWorld world { get; private set; }
		private IEcsSystems initSystems;
		private IEcsSystems updateSystems;
		private IEcsSystems fixedUpdateSystems;

		private void Start()
		{
			world = new EcsWorld();

			initSystems = new EcsSystems(world)
				.Add(new InitSystem())

				;

			initSystems.Init();
			IsReady = true;

			updateSystems = new EcsSystems(world)

#if UNITY_EDITOR
		// add debug systems for custom worlds here, for example:
		// .Add (new Leopotam.EcsLite.UnityEditor.EcsWorldDebugSystem ("events"))
		.Add(new Leopotam.EcsLite.UnityEditor.EcsWorldDebugSystem(entityNameFormat: "000", avoidhierarchy: true))
#endif
		.Add(new BattleSystem())
				;

			updateSystems.Init();

			updateSystems.Run();

			fixedUpdateSystems = new EcsSystems(world)
				;

			fixedUpdateSystems.Init();
		}

		private void Update()
		{
			updateSystems.Run();
		}

		//private void FixedUpdate()
		//{
		//	fixedUpdateSystems.Run();
		//}

		private void OnDestroy()
		{
			initSystems.Destroy();
			updateSystems.Destroy();
			fixedUpdateSystems.Destroy();
			world.Destroy();
		}
	}
}