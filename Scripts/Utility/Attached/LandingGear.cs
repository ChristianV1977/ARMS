﻿using System;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Rynchodon.Attached
{
	public class LandingGear : AttachableBlockBase
	{
		private IMyLandingGear myGear { get { return myBlock as IMyLandingGear; } }

		public LandingGear(IMyCubeBlock block)
			: base (block, AttachedGrid.AttachmentKind.LandingGear)
		{
			this.myGear.StateChanged += myGear_StateChanged;

			IMyCubeGrid attached = myGear.GetAttachedEntity() as IMyCubeGrid;
			if (attached != null)
				Attach(attached);

			myGear.OnClosing += myGear_OnClosing;
		}

		private void myGear_OnClosing(IMyEntity obj)
		{
			myGear.StateChanged -= myGear_StateChanged;
		}

		private void myGear_StateChanged(bool obj)
		{
			try
			{
				if (myGear.IsLocked)
				{
					Logger.DebugLog("Is now attached to: " + myGear.GetAttachedEntity().getBestName(), Logger.severity.DEBUG, primaryState: myGear.CubeGrid.nameWithId(), secondaryState: myGear.nameWithId());
					IMyCubeGrid attached = myGear.GetAttachedEntity() as IMyCubeGrid;
					if (attached != null)
						Attach(attached);
					else
						Detach();
				}
				else
				{
					Logger.DebugLog("Is now disconnected", Logger.severity.DEBUG, primaryState: myGear.CubeGrid.nameWithId(), secondaryState: myGear.nameWithId());
					Detach();
				}
			}
			catch (Exception ex)
			{
				Logger.AlwaysLog("Exception: " + ex, Logger.severity.ERROR, primaryState: myGear.CubeGrid.nameWithId(), secondaryState: myGear.nameWithId());
				Logger.DebugNotify("LandingGear encountered an exception", 10000, Logger.severity.ERROR);
			}
		}
	}
}
