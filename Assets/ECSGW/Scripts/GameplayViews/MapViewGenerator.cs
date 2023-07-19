using Nashet.Controllers;
using Nashet.MapMeshes;
using Nashet.MarchingSquares;
using Nashet.Utils;
using System.Collections;
using UnityEngine;

namespace Nashet.GameplayView
{
	public class MapViewGenerator : MonoBehaviour
	{
		[SerializeField] MapView mapView;
		[SerializeField] Material shoreMaterial;
		[SerializeField] GameObject r3DProvinceTextPrefab;

		private IMapController mapController;
		private int cellMultiplier = 1;

		private IEnumerator Start()
		{
			yield return new WaitWhile(() => mapController == null || !mapController.IsReady);
			GenerateView();
		}

		private void GenerateView()
		{
			CreateMap();
		}

		public void Subscribe(IMapController mapController)
		{
			this.mapController = mapController;
		}

		private void OnDestroy()
		{
			if (mapController != null)
			{

			}
		}

		private void CreateMap()
		{
			var mapTexture = PprepareTexture(null);
			UpdateStatus("Crating map mesh data..");

			var grid = new VoxelGrid(mapTexture.getWidth(), mapTexture.getHeight(), cellMultiplier * mapTexture.getWidth(), mapTexture);

			//UpdateStatus("Creating economic data..");
			//you can create world data here

			UpdateStatus("Finishing with non-unity loading..");

			foreach (var province in mapTexture.AllUniqueColors3())
			{
				var mesh = grid.getMesh(province.ToInt(), out var borderMeshes);

				//if (!IsForDeletion)
				{
					//province.
					var provinceMesh = new ProvinceMesh(province.ToInt(), mesh, borderMeshes, province, this.transform, shoreMaterial);

					var label = MapTextLabel.CreateMapTextLabel(r3DProvinceTextPrefab, province.ToString(), Color.black, provinceMesh.Position);
					label.transform.SetParent(provinceMesh.GameObject.transform, false);

				}
				//province.SetNeighbors(mesh, borderMeshes);
			}


			//Country.setMaterial();

			//todo put it in some other file. World?
			//AddRivers();
			//foreach (var province in World.AllProvinces)
			//{
			//	province.SetBorderMaterials();
			//}
		}

		private void UpdateStatus(string v)
		{

		}

		private MyTexture PprepareTexture(Texture2D mapImage)
		{
			MyTexture mapTexture;

			if (mapImage == null)
			{
				int mapSize;
				int width;
				//if (devMode)
				{
					mapSize = 20000;
					width = 150 + Rand.Get.Next(60);
				}
				//else
				//{
				//	mapSize = 40000;
				//	width = 250 + Rand.Get.Next(40);
				//}
				//mapSize = 160000;
				//width = 420;
				int amountOfProvince = mapSize / 140 + Rand.Get.Next(5);
				// amountOfProvince = 136;
				var map = new MapTextureGenerator();
				mapTexture = map.generateMapImage(width, mapSize / width, amountOfProvince);
			}
			else
			{
				//Texture2D mapImage = Resources.Load("provinces", typeof(Texture2D)) as Texture2D; ///texture;
				mapTexture = new MyTexture(mapImage);
			}
			return mapTexture; ;
		}
	}
}
