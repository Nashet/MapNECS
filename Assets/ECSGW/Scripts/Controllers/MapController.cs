using Leopotam.EcsLite;
using Nashet.Configs;
using Nashet.ECS;
using Nashet.MarchingSquares;
using Nashet.MeshData;
using Nashet.NameGeneration;
using Nashet.Services;
using Nashet.Utils;
using Nashet.Map.Utils;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;



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
		private MapGenerationConfig config;

		private readonly EcsWorld world;

		private readonly MapComponent map;
		private EcsPackedEntity? previouslySelectedUnit;
		private EcsPool<PositionComponent> positions;
		private EcsPool<ProvinceComponent> provinces;

		private int cellMultiplier = 1;

		public MapController(IConfigService configService, MapComponent map, EcsWorld world)
		{
			this.map = map;
			this.world = world;
			positions = world.GetPool<PositionComponent>();
			provinces = world.GetPool<ProvinceComponent>();
			this.configService = configService;
			config = configService.GetConfig<MapGenerationConfig>();
			IsReady = true;
		}

		public Rect GenerateWorld()
		{			
			Rect mapBorders;
			Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes;
			Dictionary<int, EcsPackedEntity> provinceLookout;
			CreateProvinces(out mapBorders, out meshes, out provinceLookout);

			SetNeighbors(provinces, meshes, provinceLookout);
			//AddRivers();

			WorldGenerated?.Invoke(world, meshes);

			return mapBorders;
		}

		private void CreateProvinces(out Rect mapBorders, out Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>> meshes, out Dictionary<int, EcsPackedEntity> provinceLookout)
		{
			HashSet<EcsPackedEntity> countriesLookup = CreateCoutries();

			provinces = world.GetPool<ProvinceComponent>();
			var mapTexture = PrepareTexture(null);
			mapBorders = new Rect(0f, 0f, mapTexture.getWidth() * cellMultiplier, mapTexture.getHeight() * cellMultiplier);
			var colors = mapTexture.AllUniqueColors3();
			var grid = new VoxelGrid(mapTexture.getWidth(), mapTexture.getHeight(), cellMultiplier * mapTexture.getWidth(), mapTexture);

			meshes = new Dictionary<int, KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>>();
			provinceLookout = new Dictionary<int, EcsPackedEntity>();

			var ecxludedColors = mapTexture.GetColorsFromBorder();
			foreach (var province in colors)
			{
				var deleteProvince = ecxludedColors.Contains(province) || Rand.Chance(config.lakeChance);
				if (deleteProvince)
					continue;
				//CreateProvince(provinces, grid, meshes, provinceLookout, province.ToInt(), countriesLookup);
				var entity = world.NewEntity();
				ref var component = ref entity.AddnSet(provinces);
				component.Id = province.ToInt();
				component.name = ProvinceNameGenerator.generateWord(6);
				component.terrain = Rand.Chance(config.mountainsChance) ? TerrainType.Mountains : TerrainType.Plains;
				component.riverNeighbors = new List<EcsPackedEntity>();
				var randomElement = Random.Range(0, countriesLookup.Count - 1);
				component.owner = countriesLookup.ElementAt(randomElement);
				var meshStructure = grid.getMesh(component.Id, out var borderMeshes);


				meshes.Add(component.Id, new KeyValuePair<MeshStructure, Dictionary<int, MeshStructure>>(meshStructure, borderMeshes));
				provinceLookout.Add(component.Id, world.PackEntity(entity));
			}
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

				var passableBorders = new List<EcsPackedEntity>();
				foreach (var item in component.phisicalNeighbors)
				{
					var neighbor = item.UnpackComponent(world, provinces);
					if (neighbor.terrain == TerrainType.Mountains && component.terrain == TerrainType.Mountains)
						continue;
					passableBorders.Add(item);
				}
				component.passableNeighbors = passableBorders.ToArray();
			}
		}

		private void AddRivers()
		{
			for (int i = 0; i < config.maxRiversAmount; i++)
			{
				var provinceFilter = world.Filter<ProvinceComponent>().End();
				
				var random = Random.Range(0, provinceFilter.GetEntitiesCount() - 1);

				int? riverStart = provinceFilter.GetEnumerable().Where(x =>
				{
					var provinceComponent = provinces.Get(x);
					return !provinceComponent.passableNeighbors.Any(y =>
					{
						var neighbor = y.UnpackComponent(world, provinces);
						return neighbor.IsRiverNeighbor(world, provinceComponent);
					});
				}).ElementAtOrDefault(random);
				//x.IsCoastal && 
				//x.Terrain == Province.TerrainTypes.Mountains &&
				if (riverStart == null)
					continue;

				ref var riverStartComponent = ref provinces.Get(riverStart.Value);

				EcsPackedEntity? riverStart2 = riverStartComponent.passableNeighbors.Random();
				//.Where(x => x.IsCoastal)
				if (riverStart2 == null)
					continue;

				riverStart2.Value.Unpack(world, out var some);
				ref var riverStartComponent2 = ref provinces.Get(some);

				AddRiverBorder(riverStartComponent, riverStartComponent2, riverStart.Value, riverStart2.Value);
			}
		}

		private void AddRiverBorder(ProvinceComponent beach1, ProvinceComponent beach2, int beach1Entity, EcsPackedEntity beach2Entity)
		{
			var logRivers = true;
			if (beach1.terrain == TerrainType.Mountains && beach2.terrain == TerrainType.Mountains)
			{
				if (logRivers)
					Debug.Log($"----river stoped because of mountain");
				return;
			}

			var chanceToContinue = Rand.Get.Next(12);
			if (chanceToContinue == 1)
			{
				if (logRivers)
					Debug.Log($"----river stoped because its long enough");
				return;
			};

			ProvinceComponent? beach3 = null;

			var potentialBeaches = beach1.passableNeighbors.Where(x =>
			{
				var comp = x.UnpackComponent(world, provinces);
				return comp.IsNeighbor(world, beach2);
			}).ToList();
			{

				if (potentialBeaches.Count == 1)
				{
					beach3 = potentialBeaches.ElementAt(0).UnpackComponent(world, provinces);
					if (beach3.Value.IsRiverNeighbor(world, beach1) || beach3.Value.IsRiverNeighbor(world, beach2))
					{
						beach3 = null;
					}
				}

				if (potentialBeaches.Count == 2)
				{
					var chooseBeach = Rand.Get.Next(2);
					if (chooseBeach == 0)
					{
						beach3 = potentialBeaches.ElementAt(0).UnpackComponent(world, provinces);
						if (beach3.Value.IsRiverNeighbor(world, beach1) || beach3.Value.IsRiverNeighbor(world, beach2))
						{
							beach3 = potentialBeaches.ElementAt(1).UnpackComponent(world, provinces);
						}
					}
					if (chooseBeach == 1)
					{
						beach3 = potentialBeaches.ElementAt(1).UnpackComponent(world, provinces);
						if (beach3.Value.IsRiverNeighbor(world, beach1) || beach3.Value.IsRiverNeighbor(world, beach2))
						{
							beach3 = potentialBeaches.ElementAt(0).UnpackComponent(world, provinces);
						}
					}
				}
			}
			if (logRivers)
				Debug.Log($"{beach1}, {beach2}");

			//meshes[beach1.Id][beach2.Id]


			beach1.riverNeighbors.Add(beach2Entity);
			var e = world.PackEntity(beach1Entity);
			beach2.riverNeighbors.Add(e);

			var chance = Rand.Get.Next(2);

			if (beach3 == null)
			{
				if (logRivers)
					Debug.Log($"----river stoped because cant find beach3");
				return;
			};
			//font knpw how to convert. Entire system is ridiculous

			//if (chance == 1 && !beach3.Value.IsRiverNeighbor(world, beach1))
			//{
			//	AddRiverBorder(beach3.Value, beach1);
			//}
			//else
			//{
			//	AddRiverBorder(beach3.Value, beach2);
			//}
		}
		private MyTexture PrepareTexture(Texture2D mapImage)
		{
			MyTexture texture = null;
			if (mapImage == null)
			{
				int height;
				int width;
				//if (devMode)
				{
					height = 130;
					width = 150 + Rand.Get.Next(60);
				}
				//else
				//{
				//	height = 160;
				//	width = 250 + Rand.Get.Next(40);
				//}
				//width = 420;
				int amountOfProvince = width * height / 140 + Rand.Get.Next(5);
				// amountOfProvince = 136;
				var mapGenerator = new MapTextureGenerator();
				texture = mapGenerator.generateMapImage(width, height, amountOfProvince);
			}
			else
			{
				//Texture2D mapImage = Resources.Load("provinces", typeof(Texture2D)) as Texture2D; ///texture;
				texture = new MyTexture(mapImage);
			}
			return texture;
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
			var cellTypeConfig = config.AllowedCellTypes.Find(x => x.Id == cellType);
			return cellTypeConfig.sprite;
		}

		public Sprite GetSprite(int x, int y)
		{
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