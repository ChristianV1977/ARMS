﻿using System.Collections.Generic;
using VRage.Collections;
using VRageMath;

namespace Rynchodon.Autopilot.Pathfinding
{
	public partial class NewPathfinder
	{

		private struct PathNode
		{
			public long ParentKey;
			public float DistToCur;
			public Vector3D Position;
			public Vector3 DirectionFromParent;
		}

		private struct PathNodeSet
		{
			public MyBinaryStructHeap<float, PathNode> m_openNodes;
			public Dictionary<long, PathNode> m_reachedNodes;
#if PROFILE
			public int m_unreachableNodes = 0;
#endif

			public PathNodeSet(bool nothing)
			{
				m_openNodes = new MyBinaryStructHeap<float, PathNode>();
				m_reachedNodes = new Dictionary<long, PathNode>();
			}

			public void Clear()
			{
				m_openNodes.Clear();
				m_reachedNodes.Clear();
#if PROFILE
				m_unreachableNodes = 0;
#endif
			}
		}

		private struct Path
		{
			public Stack<Vector3D> m_forward;
			public Queue<Vector3D> m_backward;

			public int Count { get { return m_forward.Count + m_backward.Count; } }

			public Path(bool nothing)
			{
				m_forward = new Stack<Vector3D>();
				m_backward = new Queue<Vector3D>();
			}

			public void AddFront(ref Vector3D position)
			{
				m_forward.Push(position);
			}

			public void AddBack(ref Vector3D position)
			{
				m_backward.Enqueue(position);
			}

			public void Clear()
			{
				m_forward.Clear();
				m_backward.Clear();
			}

			public void Peek(out Vector3D position)
			{
				if (m_forward.Count != 0)
					position = m_forward.Peek();
				else
					position = m_backward.Peek();
			}

			public Vector3D Peek()
			{
				if (m_forward.Count != 0)
					return m_forward.Peek();
				else
					return m_backward.Peek();
			}

			public void Pop(out Vector3D position)
			{
				if (m_forward.Count != 0)
					position = m_forward.Pop();
				else
					position = m_backward.Dequeue();
			}

			public void Pop()
			{
				if (m_forward.Count != 0)
					m_forward.Pop();
				else
					m_backward.Dequeue();
			}
		}

	}
}