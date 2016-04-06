﻿using System.Text;
using Rynchodon.Autopilot.Data;
using Rynchodon.Autopilot.Movement;
using VRageMath;

namespace Rynchodon.Autopilot.Navigator
{
	/// <summary>
	/// <para>Stops the ship</para>
	/// </summary>
	public class Stopper : NavigatorMover, INavigatorRotator
	{

		private readonly Logger _logger;
		private readonly bool m_exitAfter;

		/// <summary>
		/// Creates a new Stopper
		/// </summary>
		/// <param name="mover">The Mover to use</param>
		/// <param name="navSet">The settings to use</param>
		public Stopper(Mover mover, AllNavigationSettings navSet, bool exitAfter = false)
			: base(mover, navSet)
		{
			_logger = new Logger("Stopper", m_controlBlock.Controller);
			m_exitAfter = exitAfter;

			m_mover.MoveAndRotateStop();

			m_navSet.Settings_Task_NavRot.NavigatorMover = this;
		}

		/// <summary>
		/// Waits for the grid to stop.
		/// </summary>
		public override void Move()
		{
			// stopping in gravity does not work exactly, players should not be using EXIT anyway
			m_mover.Thrust.Update();
			float threshold = m_mover.Thrust.LocalGravity.vector != Vector3.Zero ? 0.1f : 0f;

			if (m_mover.Block.Physics.LinearVelocity.LengthSquared() <= threshold && m_mover.Block.Physics.AngularVelocity.LengthSquared() <= threshold)
			{
				INavigatorRotator rotator = m_navSet.Settings_Current.NavigatorRotator;
				if (rotator != null && !m_navSet.DirectionMatched())
				{
					_logger.debugLog("waiting for rotator to match", "Move()");
					return;
				}

				m_mover.MoveAndRotateStop();
				_logger.debugLog("stopped", "Stopper()");
				m_navSet.OnTaskComplete_NavRot();
				if (m_exitAfter)
				{
					_logger.debugLog("setting disable", "Move()", Logger.severity.DEBUG);
					m_mover.SetControl(false);
				}
			}
			else
				_logger.debugLog("linear: " + m_mover.Block.Physics.LinearVelocity + ", angular: " + m_mover.Block.Physics.AngularVelocity, "Move()");
		}

		/// <summary>
		/// Appends "Exit after stopping" or "Stopping"
		/// </summary>
		/// <param name="customInfo">The autopilot block's custom info</param>
		public override void AppendCustomInfo(StringBuilder customInfo)
		{
			if (m_exitAfter)
				customInfo.AppendLine("Exit after stopping");
			else
				customInfo.AppendLine("Stopping");
		}

		public void Rotate()
		{
			m_mover.CalcRotate_Stop();
		}

	}

}
