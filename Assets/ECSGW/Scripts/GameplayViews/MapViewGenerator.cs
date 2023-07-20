using Leopotam.EcsLite;
using Nashet.Controllers;
using Nashet.ECS;
using Nashet.MapMeshes;
using Nashet.MeshData;
using System.Collections.Generic;
using UnityEngine;

namespace Nashet.GameplayView
{
	public class MapViewGenerator : MonoBehaviour
	{
		[SerializeField] MapView mapView;
		[SerializeField] Material shoreMaterial;
		[SerializeField] GameObject r3DProvinceTextPrefab;

		private IMapController mapController;


		private void Start()
		{
			//yield return new WaitWhile(() => mapController == null || !mapController.IsReady);
			//GenerateView();
		}

		public void GenerateView(EcsWorld world, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> dict)
		{
			UpdateStatus("Crating map mesh data..");

			var provinceFilter = world.Filter<ProvinceComponent>().End();
			var provinces = world.GetPool<ProvinceComponent>();

			foreach (var province in provinceFilter)
			{
				ref var component = ref provinces.Get(province);
				
				//if (!IsForDeletion)
				{
					var provinceMesh = new ProvinceMesh(component.Id, dict[component.Id].Key, dict[component.Id].Value, Color.yellow, this.transform, shoreMaterial);

					var label = MapTextLabel.CreateMapTextLabel(r3DProvinceTextPrefab, component.name, Color.black, provinceMesh.Position); //walletComponent.name
					label.transform.SetParent(provinceMesh.GameObject.transform, false);
				}
			}

			//UpdateStatus("Creating economic data..");
			//you can create world data here

			UpdateStatus("Finishing with non-unity loading..");


			//Country.setMaterial();

			
			//foreach (var province in World.AllProvinces)
			//{
			//	province.SetBorderMaterials();
			//}
		}

		public void Subscribe(IMapController mapController)
		{
			this.mapController = mapController;
			mapController.WorldGenerated += WorldGeneratedHandler;
		}

		private void WorldGeneratedHandler(EcsWorld world, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> dict)
		{
			GenerateView(world, dict);
		}

		private void OnDestroy()
		{
			if (mapController != null)
			{

			}
		}

		private void UpdateStatus(string v)
		{

		}
	}
}
