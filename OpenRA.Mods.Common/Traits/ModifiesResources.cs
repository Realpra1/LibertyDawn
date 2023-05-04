using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor modifies resource behavior.")]
	public class ModifiesResourcesInfo : ConditionalTraitInfo
	{
		[Desc("Range of the effect.")]
		public readonly WDist Range = WDist.FromCells(6);

		[Desc("New speed growth/spread modfier in percent.")]
		public readonly int SpeedModifier = 600;

		public class ModifiesResourcesTypeInfo
		{
			[Desc("Max stage evolves into.")]
			public readonly string MaxStageEvolvesTo = null;

			[Desc("Blink interval before max stage change.")]
			public readonly int BlinkWarningInterval = 0;

			public ModifiesResourcesTypeInfo(MiniYaml yaml)
			{
				FieldLoader.Load(this, yaml);
			}
		}

		[FieldLoader.LoadUsing(nameof(LoadModifiesResourcesTypes))]
		public readonly Dictionary<string, ModifiesResourcesTypeInfo> ModifiesResourcesTypes = null;

		protected static object LoadModifiesResourcesTypes(MiniYaml yaml)
		{
			var ret = new Dictionary<string, ModifiesResourcesTypeInfo>();
			var resources = yaml.Nodes.FirstOrDefault(n => n.Key == "ModifiesResourcesTypes");
			if (resources != null)
				foreach (var r in resources.Value.Nodes)
					ret[r.Key] = new ModifiesResourcesTypeInfo(r.Value);

			return ret;
		}

		public override object Create(ActorInitializer init) { return new ModifiesResources(this); }
	}

	public class ModifiesResources : ConditionalTrait<ModifiesResourcesInfo>
	{
		public WDist Range => IsTraitDisabled ? WDist.Zero : Info.Range;

		public ModifiesResources(ModifiesResourcesInfo info)
			: base(info) { }
	}
}
