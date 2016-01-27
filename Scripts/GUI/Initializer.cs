using System;
using System.Collections.Generic;
using Rynchodon.GUI.Control;
using Rynchodon.Settings;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Gui;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Rynchodon.GUI
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Initializer : MySessionComponentBase
	{

		private enum Stage : byte { None, LoadedControls, LoadedEntities, World_Closed, Terminated }

		private readonly Logger m_logger;
		private Stage m_stage;
		private StateSaver saver;
		private HashSet<long> m_activeBlocks;

		private float minRange = 0f;
		private float maxRange = 1f;

		public Initializer()
		{
			if (IsLoaded())
			{
				m_stage = Stage.Terminated;
				return;
			}

			// Controls need to be loaded ASAP, which means there can be no logging at this point
			LoadWeapons();

			m_logger = new Logger(GetType().Namespace + '.' + GetType().Name);
			m_stage = Stage.LoadedControls;
			m_logger.debugLog("initialized", "Initializer()");
		}

		/// <summary>
		/// Initializes when the world is loaded, unless GUI is already initialized.
		/// </summary>
		public override void UpdateAfterSimulation()
		{
			if (m_stage == Stage.LoadedControls && WorldReady())
			{
				saver = new StateSaver();

				OnWorldLoad();
				maxRange = ServerSettings.GetSetting<float>(ServerSettings.SettingName.fMaxWeaponRange);
				MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;
			}
		}

		/// <summary>
		/// Verifies that MyAPIGateway fields have been filled.
		/// </summary>
		/// <returns>True if MyAPIGateway fields have been filled.</returns>
		private bool WorldReady()
		{
			return MyAPIGateway.Entities != null && MyAPIGateway.Multiplayer != null && MyAPIGateway.Utilities != null;
		}

		/// <summary>
		/// Adds entities and invokes GuiStateSaver.Load();
		/// </summary>
		private void OnWorldLoad()
		{
			AddEntities();
			saver.Load();
			m_stage = Stage.LoadedEntities;
		}

		/// <summary>
		/// Stops entities from being added and prepares Initializer for OnWorldLoad() to be invoked.
		/// </summary>
		private void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			m_stage = Stage.World_Closed;
		}

		/// <summary>
		/// Adds all the entities currently in the world and registers for entities being added.
		/// </summary>
		private void AddEntities()
		{
			m_logger.debugLog("adding entities", "AddEntities()", Logger.severity.INFO);
			if (m_activeBlocks == null)
				m_activeBlocks = new HashSet<long>();
			else
				m_activeBlocks.Clear();
			HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);
			foreach (IMyEntity ent in entities)
				OnEntityAdd(ent);
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
		}

		/// <summary>
		/// Adds all the blocks from a grid, if obj is a grid.
		/// </summary>
		/// <param name="obj">The entity that was added to the world.</param>
		private void OnEntityAdd(IMyEntity obj)
		{
			IMyCubeGrid grid = obj as IMyCubeGrid;
			if (grid == null)
				return;

			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);
			foreach (IMySlimBlock blo in blocks)
				OnBlockAdded(blo);
			grid.OnBlockAdded += OnBlockAdded;
		}

		/// <summary>
		/// Creates a TerminalBlockSync for a block, if one is required.
		/// </summary>
		/// <param name="block">The block that was added to the world.</param>
		private void OnBlockAdded(IMySlimBlock block)
		{
			if (block.FatBlock == null || !m_activeBlocks.Add(block.FatBlock.EntityId))
				return;
			block.FatBlock.OnClose += fb => m_activeBlocks.Remove(fb.EntityId);

			if (block.FatBlock is MyUserControllableGun)
			{
				m_logger.debugLog("Gun: " + block.FatBlock.DisplayNameText + '/' + block.FatBlock.DefinitionDisplayNameText + ":" + block.FatBlock.EntityId, "Entities_OnEntityAdd()");
				new TerminalBlockSync(block.FatBlock);
			}
		}

		/// <summary>
		/// Checks if terminal properties from ARMS have been loaded.
		/// </summary>
		/// <returns>True iff terminal properties from ARMS have been loaded.</returns>
		private bool IsLoaded()
		{
			List<ITerminalControl> controls = new List<ITerminalControl>();
			MyTerminalControlFactory.GetControls(typeof(MyUserControllableGun), controls);

			foreach (var cont in controls)
				if (cont.Id == "ARMS_Control")
					return true;

			return false;
		}

		/// <summary>
		/// Adds controls for weapons.
		/// </summary>
		private void LoadWeapons()
		{
			Func<MyUserControllableGun, bool> visIfTurret = gun => gun is MyLargeTurretBase;
			Func<MyUserControllableGun, bool> visIfFixed = gun => !(gun is MyLargeTurretBase);

			AddCheckbox<MyUserControllableGun>(0, "ARMS Control", "Enables ARMS control for a turret", visIfTurret);
			AddCheckbox<MyUserControllableGun>(12, "Rotor-Turret", "Enables rotor-turret function for a fixed weapon.", visIfFixed);

			AddCheckbox<MyUserControllableGun>(1, "Interior Turret", "Reduces obstruction tests");
			AddCheckbox<MyUserControllableGun>(2, "Target Functional", "Turret will shoot all functional blocks");
			AddCheckbox<MyUserControllableGun>(3, "Destroy Everything", "Turret will destroy every terminal block");

			// turrets already have these
			AddRangeSlider<MyUserControllableGun>(4, "Aiming Radius", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(5, "Target Missiles", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(6, "Target Meteors", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(7, "Target Characters", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(8, "Target Moving", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(9, "Target Large Ships", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(10, "Target Small Ships", visible: visIfFixed);
			AddOnOff<MyUserControllableGun>(11, "Target Stations", visible: visIfFixed);

			AddSeparator<MyUserControllableGun>();
		}

		/// <summary>
		/// Adds a checkbox control to a block type.
		/// </summary>
		/// <typeparam name="T">The type of block to add the checkbox to.</typeparam>
		/// <param name="index">The index of the control.</param>
		/// <param name="name">The name of the control, displayed to players.</param>
		/// <param name="tooltip">The tooltip displayed to players.</param>
		private void AddCheckbox<T>(byte index, string name, string tooltip = null, Func<T, bool> visible = null) where T : MyTerminalBlock
		{
			if (tooltip == null)
				tooltip = name;

			//m_logger.debugLog("adding checkbox for " + typeof(T) + ", index: " + index + ", name: " + name + ", tooltip: " + tooltip, "AddCheckbox()");

			TerminalControlCheckbox<T> checkbox = new TerminalControlCheckbox<T>(name.Replace(' ', '_'), MyStringId.GetOrCompute(name), MyStringId.GetOrCompute(tooltip));
			checkbox.Getter = block => GetTbsValue<bool>(block, index);
			checkbox.Setter = (block, value) => SetTbsValue(block, index, value);
			if (visible != null)
				checkbox.Visible = visible;
			checkbox.EnableAction();
			MyTerminalControlFactory.AddControl<T>(checkbox);
		}

		/// <summary>
		/// Adds an On/Off control to a block type.
		/// </summary>
		/// <typeparam name="T">The type of block to add the On/Off control to.</typeparam>
		/// <param name="index">The index of the control.</param>
		/// <param name="name">The name of the control, displayed to players.</param>
		private void AddOnOff<T>(byte index, string name, Func<T, bool> visible = null) where T : MyTerminalBlock
		{
			//m_logger.debugLog("adding on off switch for " + typeof(T) + ", index: " + index + ", name: " + name, "AddOnOff()");

			TerminalControlOnOffSwitch<T> onOff = new TerminalControlOnOffSwitch<T>(name.Replace(' ', '_'), MyStringId.GetOrCompute(name));
			onOff.Getter = block => GetTbsValue<bool>(block, index);
			onOff.Setter = (block, value) => SetTbsValue(block, index, value);
			if (visible != null)
				onOff.Visible = visible;
			onOff.EnableToggleAction();
			onOff.EnableOnOffActions();
			MyTerminalControlFactory.AddControl<T>(onOff);
		}

		/// <summary>
		/// Adds a slider control to a block type.
		/// </summary>
		/// <typeparam name="T">The type of blocks to add the slider to.</typeparam>
		/// <param name="index">The index of the control.</param>
		/// <param name="name">The name of the slider, displayed to players.</param>
		/// <param name="tooltip">The tooltip of the slider, displayed to players.</param>
		private void AddRangeSlider<T>(byte index, string name, string tooltip = null, Func<T, bool> visible = null) where T : MyTerminalBlock
		{
			if (tooltip == null)
				tooltip = name;

			//m_logger.debugLog("adding slider for " + typeof(T) + ", index: " + index + ", name: " + name, "AddRangeSlider()");

			TerminalControlSlider<T> slider = new TerminalControlSlider<T>(name.Replace(' ', '_'), MyStringId.GetOrCompute(name), MyStringId.GetOrCompute(tooltip));
			slider.Normalizer = (block, value) => NormalizeRange(value);
			slider.Denormalizer = (block, value) => DenormalizeRange(value);
			slider.DefaultValue = 0f;
			slider.Getter = (block) => GetTbsValue<float>(block, index);
			slider.Setter = (block, value) => SetTbsValue(block, index, value);
			slider.Writer = (block, result) => result.Append((int)GetTbsValue<float>(block, index)).Append(" m");
			if (visible != null)
				slider.Visible = visible;
			MyTerminalControlFactory.AddControl(slider);
		}

		private void AddSeparator<T>() where T : MyTerminalBlock
		{
			MyTerminalControlSeparator<T> separator = new MyTerminalControlSeparator<T>();
			MyTerminalControlFactory.AddControl(separator);
		}

		/// <summary>
		/// Gets a TerminalBlockSync value.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="block">The block to get the value from.</param>
		/// <param name="index">The index of the value.</param>
		/// <returns>A TerminalBlockSync value.</returns>
		private T GetTbsValue<T>(IMyEntity block, byte index)
		{
			if (saver != null)
				saver.CheckTime();

			TerminalBlockSync tbs;
			if (Registrar.TryGetValue(block, out tbs))
				return tbs.GetValue<T>(index);

			if (m_stage == Stage.World_Closed && WorldReady())
			{
				OnWorldLoad();
				return GetTbsValue<T>(block, index);
			}

			if (m_logger == null)
				throw new NullReferenceException("m_logger");
			m_logger.alwaysLog("TerminalBlockSync not in Registrar, id: " + block.EntityId, "GetTbsValue<T>()", Logger.severity.ERROR);
			return default(T);
		}

		/// <summary>
		/// Sets a TerminalBlockSync value.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="block">The block to set the value for.</param>
		/// <param name="index">The index of the value.</param>
		/// <param name="value">The value to set.</param>
		private void SetTbsValue<T>(IMyEntity block, byte index, T value)
		{
			if (saver != null)
				saver.CheckTime();

			TerminalBlockSync tbs;
			if (Registrar.TryGetValue(block, out tbs))
			{
				tbs.SetValue(index, value);
				return;
			}

			if (m_stage == Stage.World_Closed && WorldReady())
			{
				OnWorldLoad();
				SetTbsValue<T>(block, index, value);
				return;
			}

			if (m_logger == null)
				throw new NullReferenceException("m_logger");
			m_logger.alwaysLog("TerminalBlockSync not in Registrar, id: " + block.EntityId, "SetTbsValue<T>()", Logger.severity.ERROR);
		}

		/// <summary>
		/// Scales a range value to a value between 0 and 1.
		/// </summary>
		/// <param name="value">The range value.</param>
		/// <returns>A value between 0 and 1.</returns>
		private float NormalizeRange(float value)
		{
			if (value == 0)
				return 0;
			else
				return MathHelper.Clamp((value - minRange) / (maxRange - minRange), 0, 1);
		}

		/// <summary>
		/// Scales a value between 0 and 1 to a range value.
		/// </summary>
		/// <param name="value">A value between 0 and 1.</param>
		/// <returns>A range value between minRange and maxRange.</returns>
		private float DenormalizeRange(float value)
		{
			if (value == 0)
				return 0;
			else
				return MathHelper.Clamp(minRange + value * (maxRange - minRange), minRange, maxRange);
		}

	}
}
