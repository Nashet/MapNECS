using Leopotam.EcsLite;
using Nashet.Utils;

namespace Nashet.ECS
{
	public enum TerrainType
	{
		Plains,
		Mountains,
	}
	public struct ProvinceComponent
	{
		public int Id;
		public string name;
		public TerrainType terrain;
		public EcsPackedEntity[] phisicalNeighbors;
		//public EcsPackedEntity[] riverNeighbors;
		public EcsPackedEntity[] passableNeighbors;

		public EcsPackedEntity owner;

		public bool IsNeighbor(EcsWorld world, ProvinceComponent another)
		{
			var provinnces = world.GetPool<ProvinceComponent>();
			for (int i = 0; i < passableNeighbors.Length; i++)
			{
				var neighbor = passableNeighbors[i].UnpackComponent(world, provinnces);
				if (neighbor.Id == another.Id)
					return true;
			}
			return false;
		}

		public bool IsRiverNeighbor(EcsWorld world, ProvinceComponent another)
		{
			return false;
		}
	}
}