﻿using System;
using Rynchodon.Utility;
using Sandbox.ModAPI;

namespace Rynchodon.Threading
{
	public class ThreadManager
	{

		private const int QueueOverflow = 1000000;

		private static readonly int StaticMaxParallel;
		private static int StaticParallel;
		private static readonly FastResourceLock lock_parallelTasks = new FastResourceLock();

		static ThreadManager()
		{
			StaticMaxParallel = Math.Max(Environment.ProcessorCount - 2, 1);
		}

		private readonly Logger myLogger = new Logger();

		private readonly bool Background;
		private readonly string ThreadName;

		private LockedQueue<Action> ActionQueue = new LockedQueue<Action>(8);

		public readonly byte AllowedParallel;

		public byte ParallelTasks { get; private set; }

		public ThreadManager(byte AllowedParallel = 1, bool background = false, string threadName = null)
		{
			this.myLogger = new Logger(() => threadName ?? string.Empty, () => ParallelTasks.ToString());
			this.AllowedParallel = AllowedParallel;
			this.Background = background;
			this.ThreadName = threadName;

			MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;
		}

		private void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnCloseAll -= Entities_OnCloseAll;
			ActionQueue.Clear();
		}

		public void EnqueueAction(Action toQueue)
		{
			if (Globals.WorldClosed)
			{
				myLogger.debugLog("Cannot enqueue, world is closed");
				return;
			}

			ActionQueue.Enqueue(toQueue);
			VRage.Exceptions.ThrowIf<Exception>(ActionQueue.Count > QueueOverflow, "queue is too long");

			if (ParallelTasks >= AllowedParallel || StaticParallel >= StaticMaxParallel)
				return;

			MyAPIGateway.Utilities.InvokeOnGameThread(() => {
				if (MyAPIGateway.Parallel == null)
				{
					myLogger.debugLog("Parallel == null", Logger.severity.WARNING);
					return;
				}

				if (Background)
					MyAPIGateway.Parallel.StartBackground(Run);
				else
					MyAPIGateway.Parallel.Start(Run);
			});
		}

		private void Run()
		{
			using (lock_parallelTasks.AcquireExclusiveUsing())
			{
				if (ParallelTasks >= AllowedParallel || StaticParallel >= StaticMaxParallel)
					return;
				ParallelTasks++;
				StaticParallel++;
			}
			try
			{
				if (ThreadName != null)
					ThreadTracker.ThreadName = ThreadName + '(' + ThreadTracker.ThreadNumber + ')';
				Action currentItem;
				while (ActionQueue.TryDequeue(out currentItem))
				{
					if (currentItem != null)
					{
						Profiler.StartProfileBlock(currentItem);
						currentItem.Invoke();
						Profiler.EndProfileBlock();
					}
					else
						myLogger.debugLog("null action", Logger.severity.WARNING);
				}
			}
			catch (Exception ex) { myLogger.alwaysLog("Exception: " + ex, Logger.severity.ERROR); }
			finally
			{
				using (lock_parallelTasks.AcquireExclusiveUsing())
				{
					ParallelTasks--;
					StaticParallel--;
				}
				ThreadTracker.ThreadName = null;
			}
		}

	}
}
