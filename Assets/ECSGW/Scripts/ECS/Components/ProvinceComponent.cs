using Leopotam.EcsLite;

namespace Nashet.ECS
{
	public struct ProvinceComponent
	{
		public int Id;
		public string name;
		public EcsPackedEntity[] neighbors;
		public EcsPackedEntity owner;
	}
}