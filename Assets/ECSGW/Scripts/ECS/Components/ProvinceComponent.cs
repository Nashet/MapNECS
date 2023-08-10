using Leopotam.EcsLite;
using Nashet.Utils;
using System.Collections.Generic;

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
		public List<EcsPackedEntity> riverNeighbors;
		public EcsPackedEntity[] passableNeighbors;

		public EcsPackedEntity owner;

		public bool IsNeighbor(EcsWorld world, ProvinceComponent another)
		{
			var provinces = world.GetPool<ProvinceComponent>();
			for (int i = 0; i < passableNeighbors.Length; i++)
			{
				var neighbor = passableNeighbors[i].UnpackComponent(world, provinces);
				if (neighbor.Id == another.Id)
					return true;
			}
			return false;
		}

		public bool IsRiverNeighbor(EcsWorld world, ProvinceComponent another)
		{
			var provinces = world.GetPool<ProvinceComponent>();
			for (int i = 0; i < riverNeighbors.Count; i++)
			{
				var neighbor = riverNeighbors[i].UnpackComponent(world, provinces);
				if (neighbor.Id == another.Id)
					return true;
			}
			return false;
		}

		public override string ToString()
		{
			return name;
		}
	}
}