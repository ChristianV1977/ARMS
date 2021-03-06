using System.Text;
using Rynchodon.Autopilot.Data;
using Rynchodon.Autopilot.Pathfinding;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Rynchodon.Autopilot.Navigator
{
	class UnLander : ALand
	{

		private readonly Logger m_logger;
		private readonly PseudoBlock m_unlandBlock;
		/// <summary>Entity is attached entity</summary>
		private readonly Destination m_destination;
		private readonly Vector3 m_detachOffset;
		
		private bool m_attached = true;

		public UnLander(Pathfinder pathfinder, PseudoBlock unlandBlock = null)
			: base(pathfinder)
		{
			this.m_logger = new Logger(m_controlBlock.CubeBlock);
			this.m_unlandBlock = unlandBlock ?? m_navSet.Settings_Current.LandingBlock ?? m_navSet.LastLandingBlock;

			if (this.m_unlandBlock == null)
			{
				m_logger.debugLog("No unland block", Logger.severity.INFO);
				return;
			}
			m_logger.debugLog("Unland block is missing Block property", Logger.severity.FATAL, condition: this.m_unlandBlock.Block == null);

			IMyLandingGear asGear = m_unlandBlock.Block as IMyLandingGear;
			if (asGear != null)
			{
				m_destination.Entity = asGear.GetAttachedEntity();
				m_logger.debugLog("m_destination.Entity: " + m_destination.Entity);
				if (m_destination.Entity == null)
				{
					m_logger.debugLog("Not attached: " + m_unlandBlock.Block.DisplayNameText, Logger.severity.INFO);
					return;
				}
				m_logger.debugLog("Got attached entity from Landing Gear : " + m_unlandBlock.Block.DisplayNameText, Logger.severity.DEBUG);
			}
			else
			{
				IMyShipConnector asConn = m_unlandBlock.Block as IMyShipConnector;
				if (asConn != null)
				{
					m_logger.debugLog("connector");
					IMyShipConnector other = asConn.OtherConnector;
					if (other == null)
					{
						m_logger.debugLog("Not connected: " + m_unlandBlock.Block.DisplayNameText, Logger.severity.INFO);
						return;
					}
					m_logger.debugLog("Got attached connector from Connector : " + m_unlandBlock.Block.DisplayNameText, Logger.severity.DEBUG);
					m_destination.Entity = other.CubeGrid;
				}
				else
				{
					m_logger.debugLog("Cannot unland block: " + m_unlandBlock.Block.DisplayNameText, Logger.severity.INFO);
					IMyFunctionalBlock func = m_unlandBlock.Block as IMyFunctionalBlock;
					MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
						if (func != null)
							func.RequestEnable(false);
					});
					return;
				}
			}

			IMyCubeBlock block = this.m_unlandBlock.Block;
			if (block is IMyLandingGear)
				MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
					(block as IMyFunctionalBlock).RequestEnable(true);
					if ((block.GetObjectBuilderCubeBlock() as MyObjectBuilder_LandingGear).AutoLock)
						asGear.ApplyAction("Autolock");
				});

			m_detachOffset = m_unlandBlock.Block.GetPosition() - m_destination.Entity.GetPosition();

			//m_detachDirection = m_unlandBlock.WorldMatrix.Backward;
			//m_detachLength = 2f;
			//m_detatchedOffset = attachOffset + leaveDirection * (20f + m_navSet.Settings_Current.DestinationRadius);
			//m_logger.debugLog("m_detatchedOffset: " + m_detatchedOffset, "UnLander()", Logger.severity.DEBUG);
			//m_detatchDirection = attachOffset + leaveDirection

			//m_logger.debugLog("offset: " + m_detachOffset + ", direction: " + m_detachDirection);

			m_destination.Position = m_unlandBlock.WorldMatrix.Backward * 100f;

			m_navSet.Settings_Task_NavMove.NavigatorMover = this;
			m_navSet.Settings_Task_NavMove.NavigatorRotator = this;
		}

		public override void Move()
		{
			if (m_attached)
			{
				Disconnect();
				return;
			}
			else if (m_unlandBlock.Block.IsWorking)
				MyAPIGateway.Utilities.TryInvokeOnGameThread(() => ((IMyFunctionalBlock)m_unlandBlock.Block).RequestEnable(false));

			double distSqMoved = Vector3D.DistanceSquared(m_destination.Entity.GetPosition() + m_detachOffset, m_unlandBlock.WorldPosition);
			//m_navSet.Settings_Task_NavMove.Distance = (float)distanceMoved;
			if (distSqMoved > 100f)
			{
				//if (m_detachLength >= Math.Min(m_controlBlock.CubeGrid.GetLongestDim(), m_navSet.Settings_Task_NavEngage.DestinationRadius))
				//{
				MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
					(m_unlandBlock.Block as IMyFunctionalBlock).RequestEnable(false);
				});
				m_logger.debugLog("Moved away. distSqMoved: " + distSqMoved + ", dest radius: " + m_navSet.Settings_Task_NavEngage.DestinationRadius, Logger.severity.INFO);
				m_navSet.OnTaskComplete_NavMove();
				m_mover.MoveAndRotateStop(false);
				return;
				//}
				//else
				//{
				//	m_detachLength *= 1.1f;
				//	m_navSet.Settings_Current.DestinationRadius = m_detachLength * 0.5f;
				//	m_logger.debugLog("increased detach length to " + m_detachLength);
				//}
			}

			//Destination dest = new Destination(m_destination.Entity, m_detachOffset + m_detachDirection * m_detachLength);
			m_pathfinder.MoveTo(destinations: m_destination);
		}

		private void Disconnect()
		{
			IMyLandingGear asGear = m_unlandBlock.Block as IMyLandingGear;
			if (asGear != null)
			{
				m_attached = asGear.GetAttachedEntity() != null;
				if (m_attached)
				{
					m_logger.debugLog("Unlocking landing gear", Logger.severity.DEBUG);
					MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
						asGear.ApplyAction("Unlock");
					});
				}
			}
			else
			{
				IMyShipConnector asConn = m_unlandBlock.Block as IMyShipConnector;
				if (asConn != null)
				{
					m_attached = asConn.IsConnected;
					if (m_attached)
					{
						ReserveTarget(asConn.OtherConnector.EntityId);
						m_logger.debugLog("Unlocking connector", Logger.severity.DEBUG);
						MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
							asConn.RequestEnable(true);
							asConn.OtherConnector.RequestEnable(true);
							asConn.ApplyAction("Unlock");
						});
					}
				}
				else
				{
					m_logger.debugLog("cannot unlock: " + m_unlandBlock.Block.DisplayNameText, Logger.severity.INFO);
					m_attached = false;
				}
			}
		}

		public override void Rotate()
		{
			m_mover.StopRotate();
		}

		public override void AppendCustomInfo(StringBuilder customInfo)
		{
			customInfo.Append("Separating from ");
			customInfo.AppendLine(m_destination.Entity.getBestName());
			//customInfo.Append(" to ");
			//customInfo.AppendLine(m_destination.ToPretty());

			//customInfo.AppendLine(m_navSet.PrettyDistance());
		}

	}
}
