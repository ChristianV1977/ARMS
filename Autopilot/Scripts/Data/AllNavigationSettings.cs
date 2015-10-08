﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rynchodon.Autopilot.Navigator;
using Rynchodon.Settings;
using Sandbox.ModAPI;
using VRageMath;

namespace Rynchodon.Autopilot.Data
{
	public class AllNavigationSettings
	{
		//[Flags]
		//public enum PathfinderPermissions : byte
		//{
		//	None = 0,
		//	ChangeCourse = 1 << 0,
		//	All = ChangeCourse
		//}

		//[Flags]
		//public enum MovementType : byte
		//{
		//	None = 0,
		//	Rotate = 1 << 0,
		//	Move = 1 << 1,
		//	All = Rotate | Move
		//}

		public class SettingsLevel
		{
			private SettingsLevel parent;

			private IMyCubeBlock m_navigationBlock;//, m_rotationBlock;
			//private Base6Directions.Direction? m_navBlockDir, m_rotBlockDir;

			private INavigatorMover m_navigatorMover;
			private INavigatorRotator m_navigatorRotator;

			private DateTime? m_waitUntil;

			//private PathfinderPermissions? m_pathPerm;
			//private MovementType? m_allowedMovement;

			private float? m_destRadius, m_distance, m_distanceAngle, m_speedTarget; //, m_maxSpeed, m_minSpeed;

			private bool? m_ignoreAsteroid;//, m_jumpToDest;

			/// <summary>
			/// Creates the top-level SettingLevel, which has defaults set.
			/// </summary>
			internal SettingsLevel(IMyCubeBlock NavBlock)
			{
				m_navigationBlock = NavBlock;
				//m_rotationBlock = NavBlock;

				m_waitUntil = DateTime.UtcNow.AddSeconds(1);

				//m_allowedMovement = MovementType.All;
				//m_pathPerm = PathfinderPermissions.All;

				m_destRadius = 100f;
				m_distance = float.MaxValue;
				m_distanceAngle = float.MaxValue;
				m_speedTarget = 100f;
				//m_maxSpeed = ServerSettings.GetSetting<float>(ServerSettings.SettingName.fMaxSpeed);
				//m_minSpeed = 0.5f;

				m_ignoreAsteroid = false;
				//m_jumpToDest = false;
			}

			/// <summary>
			/// Creates a SettingLevel with a parent. Where values are not present, value from parent will be used.
			/// </summary>
			internal SettingsLevel(SettingsLevel parent)
			{ this.parent = parent; }

			public IMyCubeBlock NavigationBlock
			{
				get { return m_navigationBlock ?? parent.NavigationBlock; }
				set
				{
					m_navigationBlock = value;
					//m_navBlockDir = value.GetFaceDirection()[0];
				}
			}

			//public IMyCubeBlock RotationBlock
			//{
			//	get { return m_rotationBlock ?? parent.RotationBlock; }
			//	set
			//	{
			//		m_rotationBlock = value;
			//		//m_rotBlockDir = value.GetFaceDirection()[0];
			//	}
			//}

			//public Base6Directions.Direction NavBlockDir
			//{
			//	get { return (Base6Directions.Direction)(m_navBlockDir ?? parent.NavBlockDir); }
			//}

			//public Base6Directions.Direction RotBlockDir
			//{
			//	get { return (Base6Directions.Direction)(m_rotBlockDir ?? parent.RotBlockDir); }
			//}

			/// <remarks>
			/// <para>May be null</para>
			/// </remarks>
			public INavigatorMover NavigatorMover
			{
				get
				{
					if (parent == null)
						return m_navigatorMover;
					return m_navigatorMover ?? parent.NavigatorMover;
				}
				set { m_navigatorMover = value; }
			}

			/// <remarks>
			/// <para>May be null</para>
			/// </remarks>
			public INavigatorRotator NavigatorRotator
			{
				get
				{
					if (parent == null)
						return m_navigatorRotator;
					return m_navigatorRotator ?? parent.NavigatorRotator;
				}
				set { m_navigatorRotator = value; }
			}

			public DateTime WaitUntil
			{
				get { return m_waitUntil ?? parent.WaitUntil; }
				set { m_waitUntil = value; }
			}

			//public PathfinderPermissions PathPerm
			//{
			//	get { return m_pathPerm ?? parent.PathPerm; }
			//	set { m_pathPerm = value; }
			//}

			//public MovementType AllowedMovement
			//{
			//	get { return m_allowedMovement ?? parent.AllowedMovement; }
			//	set { m_allowedMovement = value; }
			//}

			public float DestinationRadius
			{
				get { return m_destRadius ?? parent.DestinationRadius; }
				set { m_destRadius = value; }
			}

			public float Distance
			{
				get { return m_distance ?? parent.Distance; }
				set { m_distance = value; }
			}

			public float DistanceAngle
			{
				get { return m_distanceAngle ?? parent.DistanceAngle; }
				set { m_distanceAngle = value; }
			}

			public float SpeedTarget
			{
				get { return m_speedTarget ?? parent.SpeedTarget; }
				set { m_speedTarget = value; }
			}

			//public float MaxSpeed
			//{
			//	get { return m_maxSpeed ?? parent.MaxSpeed; }
			//	set { m_maxSpeed = value; }
			//}

			//public float MinSpeed
			//{
			//	get { return m_minSpeed ?? parent.MinSpeed; }
			//	set { m_minSpeed = value; }
			//}

			public bool IgnoreAsteroid
			{
				get { return m_ignoreAsteroid ?? parent.IgnoreAsteroid; }
				set { m_ignoreAsteroid = value; }
			}

			//public bool JumpToDest
			//{
			//	get { return m_jumpToDest ?? parent.JumpToDest; }
			//	set { m_jumpToDest = value; }
			//}
		}

		private readonly IMyCubeBlock defaultNavBlock;

		///// <summary>Settings that are reset when Autopilot gains control. Settings should be written here but not read.</summary>
		//public SettingsLevel Settings_GainControl { get; private set; }

		/// <summary>Settings that are reset at the start of commands. Settings should be written here but not read.</summary>
		public SettingsLevel Settings_Commands { get; private set; }

		/// <summary>Settings that are reset when a primary task is completed. Settings should be written here but not read.</summary>
		public SettingsLevel Settings_Task_Primary { get; private set; }

		/// <summary>Settings that are reset when a secondary task is completed. Settings should be written here but not read.</summary>
		public SettingsLevel Settings_Task_Secondary { get; private set; }

		/// <summary>Settings that are reset when a tertiary task is completed. Settings should be written here but not read.</summary>
		public SettingsLevel Settings_Task_Tertiary { get; private set; }

		///// <summary>Settings that are reset every time autopilot is updated. Settings should be written here but not read.</summary>
		//public SettingLevel MySettings_Update { get; private set; }

		/// <summary>Reflects the current state of autopilot. Settings should be read here but not written.</summary>
		public SettingsLevel CurrentSettings { get { return Settings_Task_Tertiary; } }

		public AllNavigationSettings(IMyCubeBlock defaultNavBlock)
		{
			this.defaultNavBlock = defaultNavBlock;
			OnStartOfCommands();
		}

		//public void OnGainControl()
		//{
		//	Settings_GainControl = new SettingsLevel();
		//	OnStartOfCommands();
		//}

		public void OnStartOfCommands()
		{
			Settings_Commands = new SettingsLevel(defaultNavBlock);
			OnTaskPrimaryComplete();
		}

		public void OnTaskPrimaryComplete()
		{
			Settings_Task_Primary = new SettingsLevel(Settings_Commands);
			OnTaskSecondaryComplete();
		}

		public void OnTaskSecondaryComplete()
		{
			Settings_Task_Secondary = new SettingsLevel(Settings_Task_Primary);
			OnTaskTertiaryComplete();
		}

		public void OnTaskTertiaryComplete()
		{
			Settings_Task_Tertiary = new SettingsLevel(Settings_Task_Secondary);
			//OnUpdate();
		}

		//public void OnUpdate()
		//{
		//	MySettings_Update = new SettingLevel(MySettings_Subtask);
		//}

	}
}
