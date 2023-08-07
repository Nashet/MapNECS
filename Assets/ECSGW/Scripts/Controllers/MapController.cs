using Leopotam.EcsLite;
using Nashet.Configs;
using Nashet.ECS;
using Nashet.MarchingSquares;
using Nashet.MeshData;
using Nashet.NameGeneration;
using Nashet.Services;
using Nashet.Utils;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;
using Nashet.Map.Utils;

namespace Nashet.Controllers
{
	public delegate void OnUnitClicked(Vector2Int position, Vector3 worldPosition);
	public delegate void OnWaypointsRefresh(HashSet<Vector2Int> wayPoints);
	public delegate void OnExplosionHappened(Vector2Int where, int amount);
	public delegate void OnUnitAppeared(Vector2Int position, string unitType);
	public delegate void OnUnitMoved(Vector2Int from, Vector2Int toPosition);

	public delegate void OnWorldgenerated(EcsWorld world, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> dict);
	public enum Direction { horizontal, vertical }

	public class MapController : IMapController
	{
		public event OnExplosionHappened ExplosionHappened;
		public event OnUnitAppeared UnitAppeared;
		public event OnUnitClicked UnitClicked;
		public event OnUnitClicked EmptyCellClicked;
		public event OnWaypointsRefresh WaypointsRefresh;
		public event OnUnitMoved UnitMoved;

		public event OnWorldgenerated WorldGenerated;
		public bool IsReady { get; private set; }

		private readonly IConfigService configService;
		private readonly MapComponent map;
		private readonly EcsWorld world;
		private EcsPackedEntity? previouslySelectedUnit;
		private int cellMultiplier = 1;
		private EcsPool<PositionComponent> positions;

		public MapController(IConfigService configService, MapComponent map, EcsWorld world)
		{
			this.map = map;
			this.world = world;
			positions = world.GetPool<PositionComponent>();
			this.configService = configService;

			IsReady = true;
		}

		public Rect GenerateWorld()
		{
			HashSet<EcsPackedEntity> countriesLookup = CreateCoutries();

			var provinces = world.GetPool<ProvinceComponent>();
			var mapTexture = PprepareTexture(null);
			var mapBorders = new Rect(0f, 0f, mapTexture.getWidth() * cellMultiplier, mapTexture.getHeight() * cellMultiplier);
			var colors = mapTexture.AllUniqueColors3();
			var grid = new VoxelGrid(mapTexture.getWidth(), mapTexture.getHeight(), cellMultiplier * mapTexture.getWidth(), mapTexture);

			var meshes = new Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>>();
			var provinceLookout = new Dictionary<int, EcsPackedEntity>();

			//AddRivers();
			var ecxludedColors = mapTexture.GetColorsFromBorder();
			foreach (var province in colors)
			{
				if (ecxludedColors.Contains(province))
					continue;
				CreateProvince(provinces, grid, meshes, provinceLookout, province.ToInt(), countriesLookup);
			}

			SetNeighbors(provinces, meshes, provinceLookout);



			WorldGenerated?.Invoke(world, meshes);

			return mapBorders;
		}

		private HashSet<EcsPackedEntity> CreateCoutries()
		{
			var countriesLookup = new HashSet<EcsPackedEntity>();
			var countries = world.GetPool<CountryComponent>();
			for (int i = 0; i < 8; i++)
			{
				var entity = world.NewEntity();
				ref var component = ref entity.AddnSet(countries);
				component.name = CountryNameGenerator.generateCountryName();
				component.color = ColorExtensions.getRandomColor();
				component.Id = i;
				countriesLookup.Add(world.PackEntity(entity));
			}

			return countriesLookup;
		}

		private void CreateProvince(EcsPool<ProvinceComponent> provinces, VoxelGrid grid,
			Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes,
			Dictionary<int, EcsPackedEntity> entityLookout, int Id, HashSet<EcsPackedEntity> countries)
		{
			var entity = world.NewEntity();
			ref var component = ref entity.AddnSet(provinces);
			component.Id = Id;
			component.name = ProvinceNameGenerator.generateWord(6);
			var randomElement = Random.Range(0, countries.Count - 1);
			component.owner = countries.ElementAt(randomElement);
			var meshStructure = grid.getMesh(component.Id, out var borderMeshes);


			meshes.Add(component.Id, new KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>(meshStructure, borderMeshes));
			entityLookout.Add(component.Id, world.PackEntity(entity));
		}

		private void SetNeighbors(EcsPool<ProvinceComponent> provinces, Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes, Dictionary<int, EcsPackedEntity> entityLookout)
		{
			var provinceFilter = world.Filter<ProvinceComponent>().End();
			foreach (var province in provinceFilter)
			{
				ref var component = ref provinces.Get(province);
				var borderMeshes = meshes[component.Id];

				int i = 0;
				foreach (var item in borderMeshes.Value.Keys)
				{
					if (entityLookout.TryGetValue(item, out var lookOut))
					{
						i++;
					}
				}
				component.phisicalNeighbors = new EcsPackedEntity[i]; //cause idk how much provinces was deleted
				i = 0;
				foreach (var item in borderMeshes.Value.Keys)
				{
					if (entityLookout.TryGetValue(item, out var lookOut))
					{
						component.phisicalNeighbors[i] = entityLookout[item];
						i++;
					}
				}
			}
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

		public void CreateUnits()
		{

			var filter = world.Filter<UnitTypeComponent>().Inc<PositionComponent>().End();
			var positions = world.GetPool<PositionComponent>();
			var types = world.GetPool<UnitTypeComponent>();


			foreach (int entity in filter)
			{
				ref var position = ref positions.Get(entity);
				ref var type = ref types.Get(entity);

				UnitAppeared?.Invoke(position.pos, type.unitId);
			}
		}

		private void ExplosionHappenedHandler(Vector2Int where, int amount)
		{
			ExplosionHappened?.Invoke(where, amount);
		}

		public void SimulateOneStep()
		{
			//map.CheckRowsForCoincidence();
		}

		public Sprite GetSprite(string cellType)
		{
			var config = configService.GetConfig<MapGenerationConfig>();
			var cellTypeConfig = config.AllowedCellTypes.Find(x => x.Id == cellType);
			return cellTypeConfig.sprite;
		}

		public Sprite GetSprite(int x, int y)
		{
			//todo fix taht
			var config = configService.GetConfig<MapGenerationConfig>();
			var cellType = map.GetElement(x, y);
			var cellTypeConfig = config.AllowedCellTypes.Find(x => x.Id == cellType);
			return cellTypeConfig.sprite;
		}

		public Vector2Int GetSize()
		{
			return new Vector2Int(map.xSize, map.ySize);
		}

		public string GetType(int x, int y)
		{
			return map.GetElement(x, y);
		}

		public bool IsPossibleSlide(Vector2Int from, Vector2Int to)
		{
			return false;// map.IsPossibleSlide(from, to);//todo fix
		}

		public void MakeSlide(Vector2Int from, Vector2Int to)
		{
			//map.MakeSlide(from, to);
		}

		public void RefreshView()
		{

		}

		internal void HandleCellClicked(Vector2Int clickedCell, Vector3 worldPosition)
		{
			// I can caсhe it in mapComponent
			//var cell = map.GetElement(position.x, position.y);
			var filter = world.Filter<UnitTypeComponent>().Inc<PositionComponent>().End();

			var types = world.GetPool<UnitTypeComponent>();

			bool clickedOnEmptyCell = true;

			foreach (int entity in filter)
			{
				ref var position = ref positions.Get(entity);
				if (position.pos == clickedCell)
				{
					ref var type = ref types.Get(entity);
					map.CopyTo(position, clickedCell);

					UnitClicked?.Invoke(clickedCell, worldPosition);

					RefreshWaypoints(clickedCell);

					if (previouslySelectedUnit.HasValue)
					{
						previouslySelectedUnit.Value.Unpack(world, out int unpackedEntity);
						if (entity == unpackedEntity)
						{
							WaypointsRefresh?.Invoke(null);
							previouslySelectedUnit = null;
							return;
						}
					}
					previouslySelectedUnit = world.PackEntity(entity);

					clickedOnEmptyCell = false;
					break;
				}
			}

			if (clickedOnEmptyCell)
			{
				ClickedOnEmptyCellHandler(clickedCell, worldPosition);
			}
		}

		private void RefreshWaypoints(Vector2Int clickedCell)
		{
			var list = NearByPoints2(clickedCell);
			list.Remove(clickedCell);
			WaypointsRefresh?.Invoke(list);
		}

		private void ClickedOnEmptyCellHandler(Vector2Int clickedCell, Vector3 worldPosition)
		{
			if (previouslySelectedUnit != null)
			{
				var fromPosition = previouslySelectedUnit.Value.UnpackComponent(world, positions);
				MoveUnit(previouslySelectedUnit.Value, fromPosition.pos, clickedCell);
				previouslySelectedUnit = null;
			}
			else
			{
				EmptyCellClicked?.Invoke(clickedCell, worldPosition);
			}
		}

		private void MoveUnit(EcsPackedEntity entity, Vector2Int from, Vector2Int toPosition)
		{
			entity.Unpack(world, out int unpackedEntity);
			ref var position = ref positions.Get(unpackedEntity);
			position.pos = toPosition;
			UnitMoved?.Invoke(from, toPosition);
		}

		private HashSet<Vector2Int> NearByPoints2(Vector2Int pos)
		{
			var firstRing = NearByPoints(pos);
			var result = new HashSet<Vector2Int>();
			foreach (var item in firstRing)
			{
				var b = NearByPoints(item);
				result.AddRange(b);
			}
			result.AddRange(firstRing);
			return result;
		}
		private HashSet<Vector2Int> NearByPoints(Vector2Int pos)
		{
			return new HashSet<Vector2Int> {
			new Vector2Int(pos.x, pos.y - 1),
			new Vector2Int(pos.x-1, pos.y),
			new Vector2Int(pos.x, pos.y + 1),
			new Vector2Int(pos.x+1, pos.y) };
		}
	}

	public interface IMapController
	{
		bool IsReady { get; }

		event OnExplosionHappened ExplosionHappened;
		event OnUnitAppeared UnitAppeared;
		event OnWaypointsRefresh WaypointsRefresh;
		event OnUnitMoved UnitMoved;
		event OnWorldgenerated WorldGenerated;

		void SimulateOneStep();

		//todo put in a separate controller
		void RefreshView()
		{ }

		Sprite GetSprite(int x, int y);
		Vector2Int GetSize();
		string GetType(int x, int y);
		Sprite GetSprite(string cellType);
		void CreateUnits();
	}
}