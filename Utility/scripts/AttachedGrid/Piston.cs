﻿using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

namespace Rynchodon.AttachedGrid
{
	public static class Piston
	{
		public class PistonBase : AttachableBlockPair
		{
			private readonly Logger myLogger;

			public PistonBase(IMyCubeBlock block)
				: base(block, AttachedGrid.AttachmentKind.Piston)
			{
				myLogger = new Logger("PistonBase", block);
			}

			protected override AttachableBlockPair GetPartner()
			{
				var builder = myBlock.GetSlimObjectBuilder_Safe() as MyObjectBuilder_ExtendedPistonBase;
				if (builder == null)
					throw new NullReferenceException("builder");
				return GetPartner(builder.TopBlockId);
			}
		}

		public class PistonTop : AttachableBlockPair
		{
			private readonly Logger myLogger;

			public PistonTop(IMyCubeBlock block)
				: base(block, AttachedGrid.AttachmentKind.Piston)
			{
				myLogger = new Logger("PistonTop", block);
			}

			protected override AttachableBlockPair GetPartner()
			{
				var builder = myBlock.GetSlimObjectBuilder_Safe() as MyObjectBuilder_PistonTop;
				if (builder == null)
					throw new NullReferenceException("builder");
				return GetPartner(builder.PistonBlockId);
			}
		}
	}
}
