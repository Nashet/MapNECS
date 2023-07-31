using Nashet.Controllers;
using Nashet.ECS;
using Nashet.GameplayView;
using Nashet.Map.GameplayControllers;
using Nashet.Services;
using Nashet.Utils;
using System.Collections;
using UnityEngine;

namespace Nashet.Initialization
{
	public class MonoInjector : MonoBehaviour
	{
		[SerializeField] ECSRunner ECSRunner;
		[SerializeField] MapView mapView;
		[SerializeField] MapViewSound mapViewSound;
		[SerializeField] MapViewGenerator mapViewGenerator;
		[SerializeField] DebugClicker debugClicker;
		[SerializeField] IScoresView scoresView;
		[SerializeField] SelectedUnitView selectedUnitView;
		[SerializeField] CameraController cameraController;

		private IEnumerator Start()
		{
			yield return new WaitUntil(() => ServiceLocator.Instance.initialized);
			yield return new WaitUntil(() => ECSRunner.IsReady);

			var configService = ServiceLocator.Instance.Get<IConfigService>();

			var map = ECSRunner.world.GetSingleComponent<MapComponent>();

			var mapController = new MapController(configService, map, ECSRunner.world);


			mapViewGenerator.Subscribe(mapController);
			mapView.Subscribe(mapController);
			var mapBorders = mapController.GenerateWorld();
			cameraController.Initialize(mapBorders);
			new SoundController(mapViewSound);

			debugClicker.SimulateStepHappened += mapController.SimulateOneStep;//TODO its better to subscribe inside controller


			var scoresController = new ScoresController(scoresView);//new ScoresModel()
			scoresController.Subscribe(mapController);
			//scoresView.Subscribe(scoresController);
		}
	}
}
