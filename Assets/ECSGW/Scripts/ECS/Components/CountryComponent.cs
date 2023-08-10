using UnityEngine;

namespace Nashet.ECS
{
	public struct CountryComponent
	{
		public string name;
		public Color color;
		public int Id;

		public override string ToString()
		{
			return name;
		}
	}
}