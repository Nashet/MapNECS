using Leopotam.EcsLite;
using Nashet.Controllers;
using Nashet.ECS;
using Nashet.Map.Utils;
using Nashet.MapMeshes;
using Nashet.MeshData;
using Nashet.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Nashet.GameplayView
{
	public class MapViewGenerator : MonoBehaviour
	{
		[SerializeField] MapView mapView;
		[SerializeField] Material shoreMaterial;
		[SerializeField] Material defaultProvinceBorderMaterial;
		[SerializeField] Material riverBorder;
		[SerializeField] Material impassableBorder;
		[SerializeField] Material defaultCountryBorderMaterial;
		[SerializeField] GameObject r3DProvinceTextPrefab;

		private IMapController mapController;
		private EcsPool<ProvinceComponent> provinces;
		private EcsPool<CountryComponent> countries;
		private Dictionary<int, Material> countryBorderMaterials = new Dictionary<int, Material>();

		private void Start()
		{
			//yield return new WaitWhile(() => mapController == null || !mapController.IsReady);
			//GenerateView();
		}

		private void OnDestroy()
		{
			if (mapController != null)
			{

			}
		}

		public void GenerateView(EcsWorld world, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes)
		{
			UpdateStatus("Crafting map mesh data..");

			var provinceFilter = world.Filter<ProvinceComponent>().End();
			provinces = world.GetPool<ProvinceComponent>();
			countries = world.GetPool<CountryComponent>();
			var countryFilter = world.Filter<CountryComponent>().End();

			SetCountries(countryFilter);

			SetProvinces(world, meshes, provinceFilter);

			//UpdateStatus("Creating economic data..");
			//you can create world data here

			UpdateStatus("Finishing with non-unity loading..");


			//Country.setMaterial();
		}

		private void SetProvinces(EcsWorld world, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes, EcsFilter provinceFilter)
		{
			foreach (var province in provinceFilter)
			{
				ref var component = ref provinces.Get(province);
				component.owner.Unpack(world, out var owner);
				var country = countries.Get(owner);
				var provinceMesh = new ProvinceMesh(component.Id, meshes[component.Id].Key, meshes[component.Id].Value, country.color.getAlmostSameColor(), this.transform, shoreMaterial);

				var label = MapTextLabel.CreateMapTextLabel(r3DProvinceTextPrefab, provinceMesh.Position, component.name, Color.black);
				label.transform.SetParent(provinceMesh.GameObject.transform, false);

				SetInitialBorderMaterial(world, component, provinceMesh, component.phisicalNeighbors);
			}
		}

		private void SetCountries(EcsFilter countryFilter)
		{
			foreach (var country in countryFilter)
			{
				ref var component = ref countries.Get(country);
				var bordermaterial = new Material(defaultCountryBorderMaterial) { color = component.color.getNegative() };

				countryBorderMaterials.Add(component.Id, bordermaterial);
			}
		}

		private void SetInitialBorderMaterial(EcsWorld world, ProvinceComponent province, ProvinceMesh provinceMesh, EcsPackedEntity[] physicalNeighbors)
		{
			foreach (var neighbors in physicalNeighbors)
			{
				var neighbor = neighbors.UnpackComponent(world, provinces);

				if (neighbor.IsNeighbor(world, province)) //meaning if it has a passage
				{
					if (neighbor.IsRiverNeighbor(world, province))
					{
						provinceMesh.SetBorderMaterial(neighbor.Id, riverBorder);
					}
					else
					{
						if (province.owner.Equals(neighbor.owner)) // same country
						{
							provinceMesh.SetBorderMaterial(neighbor.Id, defaultProvinceBorderMaterial);
						}
						else
						{
							if (province.owner.Equals(null))
							{
								provinceMesh.SetBorderMaterial(neighbor.Id, defaultProvinceBorderMaterial);
							}
							else
							{
								//province.owner.Unpack(world, out var country);
								var country = province.owner.UnpackComponent(world, countries);
								provinceMesh.SetBorderMaterial(neighbor.Id, countryBorderMaterials[country.Id]);
							}
						}
					}
				}
				else
				{
					provinceMesh.SetBorderMaterial(neighbor.Id, impassableBorder);
				}
			}
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

		/// <summary>
		/// Suposed to update loading status...
		/// </summary>
		/// <param name="v"></param>
		private void UpdateStatus(string v)
		{
		
		}
	}
}
