﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using System.Linq;
using VRageMath;
using Rynchodon.Utility.Vectors;
using VRage.Game.Entity;
using Sandbox.Game.Entities.Cube;
using VRage.Collections;

namespace Rynchodon
{
	/// <summary>
	/// A better way to get cube blocks of type from a grid.
	/// </summary>
	public class CubeGridCache
	{

		private static FastResourceLock lock_constructing = new FastResourceLock();
		public static LockedDictionary<MyDefinitionId, string> DefinitionType = new LockedDictionary<MyDefinitionId, string>();

		/// <summary>
		/// will return null if grid is closed or CubeGridCache cannot be created
		/// </summary>
		public static CubeGridCache GetFor(IMyCubeGrid grid)
		{
			if (grid.Closed || grid.MarkedForClose || Globals.WorldClosed)
				return null;

			CubeGridCache value;
			if (Registrar.TryGetValue(grid.EntityId, out value))
				return value;

			using (lock_constructing.AcquireExclusiveUsing())
			{
				if (Registrar.TryGetValue(grid.EntityId, out value))
					return value;

				try
				{ return new CubeGridCache(grid); }
				catch (Exception e)
				{
					Logger.AlwaysLog("Exception on creation: " + e, Logger.severity.WARNING);
					return null;
				}
			}
		}

		private readonly Logger myLogger;
		private readonly IMyCubeGrid CubeGrid;

		private Dictionary<MyObjectBuilderType, List<MySlimBlock>> CubeBlocks = new Dictionary<MyObjectBuilderType, List<MySlimBlock>>();
		private FastResourceLock lock_blocks = new FastResourceLock();

		public int CellCount { get; private set; }
		public int TerminalBlocks { get; private set; }

		private CubeGridCache(IMyCubeGrid grid)
		{
			myLogger = new Logger(() => grid.DisplayName);
			CubeGrid = grid;
			List<IMySlimBlock> allSlims = new List<IMySlimBlock>();
			CubeGrid.GetBlocks_Safe(allSlims);

			using (lock_blocks.AcquireExclusiveUsing())
				foreach (IMySlimBlock slim in allSlims)
					Add(slim);

			CubeGrid.OnBlockAdded += CubeGrid_OnBlockAdded;
			CubeGrid.OnBlockRemoved += CubeGrid_OnBlockRemoved;
			CubeGrid.OnClosing += CubeGrid_OnClosing;

			Registrar.Add(CubeGrid, this);
			myLogger.debugLog("built for: " + CubeGrid.DisplayName, Logger.severity.DEBUG);
		}

		private void CubeGrid_OnClosing(IMyEntity grid)
		{
			CubeGrid.OnBlockAdded -= CubeGrid_OnBlockAdded;
			CubeGrid.OnBlockRemoved -= CubeGrid_OnBlockRemoved;
			CubeGrid.OnClosing -= CubeGrid_OnClosing;
		}

		private void CubeGrid_OnBlockAdded(IMySlimBlock obj)
		{
			lock_blocks.AcquireExclusive();
			try { Add(obj); }
			catch (Exception e) { myLogger.alwaysLog("Exception: " + e, Logger.severity.ERROR); }
			finally { lock_blocks.ReleaseExclusive(); }
		}

		private void Add(IMySlimBlock obj, bool fromAddInitialized = false)
		{
			MySlimBlock block = (MySlimBlock)obj;
			MyObjectBuilderType typeId = block.BlockDefinition.Id.TypeId;
			List<MySlimBlock> blockList;
			if (!CubeBlocks.TryGetValue(typeId, out blockList))
			{
				blockList = new List<MySlimBlock>();
				CubeBlocks.Add(typeId, blockList);
			}
			myLogger.debugLog("already in blockList: " + obj.nameWithId(), Logger.severity.ERROR, condition: blockList.Contains(block));
			blockList.Add(block);

			IMyTerminalBlock term = block.FatBlock as IMyTerminalBlock;
			if (term != null)
			{
				if (DefinitionType.TrySet(((MyCubeBlock)term).BlockDefinition.Id, term.DefinitionDisplayNameText))
					myLogger.debugLog("new type: " + term.DefinitionDisplayNameText, Logger.severity.DEBUG);
				TerminalBlocks++;
			}

			Vector3I cellSize = block.Max - block.Min + 1;
			CellCount += cellSize.X * cellSize.Y * cellSize.Z;
		}

		private void CubeGrid_OnBlockRemoved(IMySlimBlock obj)
		{
			lock_blocks.AcquireExclusive();
			try
			{
				MySlimBlock block = (MySlimBlock)obj;
				MyObjectBuilderType typeId = block.BlockDefinition.Id.TypeId;
				List<MySlimBlock> blockList;
				if (!CubeBlocks.TryGetValue(typeId, out blockList))
				{
					myLogger.debugLog("failed to get list of type: " + typeId, Logger.severity.WARNING);
					return;
				}
				if (!blockList.Remove(block))
				{
					myLogger.debugLog("already removed: " + obj.nameWithId(), Logger.severity.WARNING);
					return;
				}

				myLogger.debugLog("block removed: " + obj.nameWithId(), Logger.severity.TRACE);
				//Logger.DebugNotify("block removed: " + obj.getBestName(), level: Logger.severity.TRACE);

				if (blockList.Count == 0)
					CubeBlocks.Remove(typeId);
				if (block.FatBlock is IMyTerminalBlock)
					TerminalBlocks--;

				Vector3I cellSize = block.Max - block.Min + 1;
				CellCount -= cellSize.X * cellSize.Y * cellSize.Z;
			}
			catch (Exception e) { myLogger.alwaysLog("Exception: " + e, Logger.severity.ERROR); }
			finally { lock_blocks.ReleaseExclusive(); }
		}

		public IEnumerable<MyCubeBlock> BlocksOfType(MyObjectBuilderType typeId)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(typeId, out blockList))
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MyCubeBlock cubeBlock = blockList[i].FatBlock;
						if (cubeBlock != null)
							yield return cubeBlock;
					}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<MySlimBlock> SlimBlocksOfType(MyObjectBuilderType typeId)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(typeId, out blockList))
					for (int i = blockList.Count - 1; i >= 0; i--)
						yield return blockList[i];
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<MyCubeBlock> BlocksOfType(MyDefinitionId defId)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(defId.TypeId, out blockList))
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MySlimBlock block = blockList[i];
						MyCubeBlock cubeBlock = block.FatBlock;
						if (cubeBlock != null && !cubeBlock.Closed && block.BlockDefinition.Id.SubtypeId == defId.SubtypeId)
							yield return cubeBlock;
					}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<MySlimBlock> SlimBlocksOfType(MyDefinitionId defId)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(defId.TypeId, out blockList))
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MySlimBlock block = blockList[i];
						if (!block.Closed() && block.BlockDefinition.Id.SubtypeId == defId.SubtypeId)
							yield return block;
					}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<MyCubeBlock> AllCubeBlocks()
		{
			lock_blocks.AcquireShared();
			try
			{
				foreach (List<MySlimBlock> blockList in CubeBlocks.Values)
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MyCubeBlock cubeBlock = blockList[i].FatBlock;
						if (cubeBlock != null)
							yield return cubeBlock;
					}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<IMySlimBlock> AllSlimBlocks()
		{
			lock_blocks.AcquireShared();
			try
			{
				foreach (List<MySlimBlock> blockList in CubeBlocks.Values)
					for (int i = blockList.Count - 1; i >= 0; i--)
						yield return blockList[i];
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public IEnumerable<Vector3I> OccupiedCells()
		{
			lock_blocks.AcquireShared();
			try
			{
				Matrix invLocal = new Matrix();

				foreach (List<MySlimBlock> blockList in CubeBlocks.Values)
				{
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MySlimBlock slim = blockList[i];
						if (slim.Closed())
						{
							myLogger.debugLog("is closed: " + slim); // rare if blocks are being removed correctly
							continue;
						}

						MyCubeBlock cubeBlock = slim.FatBlock;
						if (cubeBlock is IMyDoor && cubeBlock.Subparts.Count != 0)
						{
							foreach (MyEntitySubpart part in cubeBlock.Subparts.Values)
								yield return slim.CubeGrid.WorldToGridInteger(part.PositionComp.GetPosition());
							continue;
						}

						// for piston base and stator, cell may not actually be inside local AABB
						// if this is done for doors, they would always be treated as open
						// other blocks have not been tested
						bool checkLocal = cubeBlock is IMyMotorStator || cubeBlock is IMyPistonBase;

						if (checkLocal)
							invLocal = Matrix.Invert(cubeBlock.PositionComp.LocalMatrix);

						Vector3I cell;
						for (cell.X = slim.Min.X; cell.X <= slim.Max.X; cell.X++)
							for (cell.Y = slim.Min.Y; cell.Y <= slim.Max.Y; cell.Y++)
								for (cell.Z = slim.Min.Z; cell.Z <= slim.Max.Z; cell.Z++)
								{
									if (checkLocal)
									{
										Vector3 posGrid = cell * slim.CubeGrid.GridSize;
										Vector3 posBlock;
										Vector3.Transform(ref posGrid, ref invLocal, out posBlock);
										if (cubeBlock.PositionComp.LocalAABB.Contains(posBlock) == ContainmentType.Disjoint)
											continue;
									}
									yield return cell;
								}
					}
				}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public int CountByType(MyObjectBuilderType typeId)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(typeId, out blockList))
					return blockList.Count;
				else
					return 0;
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		public int CountByType(MyDefinitionId defId)
		{
			int count = 0;
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(defId.TypeId, out blockList))
					for (int i = blockList.Count - 1; i >= 0; i--)
					{
						MySlimBlock block = blockList[i];
						if (!block.Closed() && block.BlockDefinition.Id.SubtypeId == defId.SubtypeId)
							count++;
					}
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
			return count;
		}

		/// <summary>
		/// Count the number of blocks that match a particular condition.
		/// </summary>
		/// <param name="objBuildType">Type to search for</param>
		/// <param name="condition">Condition that block must match</param>
		/// <returns>The number of blocks of the given type that match the condition.</returns>
		public int CountByType(MyObjectBuilderType objBuildType, Func<IMyCubeBlock, bool> condition, int stopCaringAt = int.MaxValue)
		{
			lock_blocks.AcquireShared();
			try
			{
				List<MySlimBlock> blockList;
				if (CubeBlocks.TryGetValue(objBuildType, out blockList))
				{
					int count = 0;
					foreach (MySlimBlock block in blockList)
					{
						MyCubeBlock cubeBlock = block.FatBlock;
						if (cubeBlock != null && condition(cubeBlock))
						{
							count++;
							if (count >= stopCaringAt)
								return count;
						}
					}

					return count;
				}
				return 0;
			}
			finally
			{
				lock_blocks.ReleaseShared();
			}
		}

		/// <summary>
		/// Gets the closest occupied cell.
		/// </summary>
		public Vector3I GetClosestOccupiedCell(Vector3I startCell, Vector3I previousCell)
		{
			Vector3I closestCell = previousCell;
			int closestDistance = CubeGrid.GetCubeBlock(closestCell) != null ? previousCell.DistanceSquared(startCell) - 2 : int.MaxValue;

			CubeGridCache cache = CubeGridCache.GetFor(CubeGrid);
			if (cache == null)
				return closestCell;

			foreach (Vector3I cell in cache.OccupiedCells())
			{
				int dist = cell.DistanceSquared(startCell);
				if (dist < closestDistance)
				{
					closestCell = cell;
					closestDistance = dist;
				}
			}

			return closestCell;
		}

		/// <summary>
		/// Gets the closest occupied cell.
		/// </summary>
		public Vector3I GetClosestOccupiedCell(Vector3D startWorld, Vector3I previousCell)
		{
			return GetClosestOccupiedCell(CubeGrid.WorldToGridInteger(startWorld), previousCell);
		}

	}
}
