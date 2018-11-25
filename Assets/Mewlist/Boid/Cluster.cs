using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Mewlist.Boid
{
    public struct AgentPosition : IEquatable<int>
    {
        public int index;
        public float3 position;

        public bool Equals(int other)
        {
            return index == other;
        }

        public override int GetHashCode()
        {
            return index;
        }
    }

    public struct Cluster
    {
        [NativeDisableParallelForRestriction] private NativeArray<AgentPosition> xSorted;
        [NativeDisableParallelForRestriction] private NativeArray<AgentPosition> ySorted;
        [NativeDisableParallelForRestriction] private NativeArray<AgentPosition> zSorted;
        [NativeDisableParallelForRestriction] private NativeHashMap<int, int> xMap;
        [NativeDisableParallelForRestriction] private NativeHashMap<int, int> yMap;
        [NativeDisableParallelForRestriction] private NativeHashMap<int, int> zMap;

        private readonly int maxAgents;

        public Cluster(int maxAgents)
        {
            this.maxAgents = maxAgents;
            xSorted = new NativeArray<AgentPosition>(maxAgents, Allocator.Persistent);
            ySorted = new NativeArray<AgentPosition>(maxAgents, Allocator.Persistent);
            zSorted = new NativeArray<AgentPosition>(maxAgents, Allocator.Persistent);
            xMap = new NativeHashMap<int, int>(maxAgents, Allocator.Persistent);
            yMap = new NativeHashMap<int, int>(maxAgents, Allocator.Persistent);
            zMap = new NativeHashMap<int, int>(maxAgents, Allocator.Persistent);
        }

        public void Release()
        {
            if (xSorted.IsCreated) xSorted.Dispose();
            if (ySorted.IsCreated) ySorted.Dispose();
            if (zSorted.IsCreated) zSorted.Dispose();
            if (xMap.IsCreated) xMap.Dispose();
            if (yMap.IsCreated) yMap.Dispose();
            if (zMap.IsCreated) zMap.Dispose();
        }

        public void SetAgentPositions(ComponentDataArray<Position> positions)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                xSorted[i] = new AgentPosition()
                {
                    index = i,
                    position = positions[i].Value
                };
            }

            ySorted.CopyFrom(xSorted);
            zSorted.CopyFrom(xSorted);
            xSorted.Sort(new XComparer());
            ySorted.Sort(new YComparer());
            zSorted.Sort(new ZComparer());
            for (var i = 0; i < xSorted.Length; i++) xMap.TryAdd(xSorted[i].index, i);
            for (var i = 0; i < ySorted.Length; i++) yMap.TryAdd(ySorted[i].index, i);
            for (var i = 0; i < zSorted.Length; i++) zMap.TryAdd(zSorted[i].index, i);
        }

        // 各軸毎に距離の近い Agent を maxCount を上限に探す
        public void SearchNeighbors(int index, float3 dir, ref HashSet<int> list, float maxDist, int maxCount)
        {
            int xIndex;
            int yIndex;
            int zIndex;
            xMap.TryGetValue(index, out xIndex);
            yMap.TryGetValue(index, out yIndex);
            zMap.TryGetValue(index, out zIndex);

            var xd = dir.x >= 0 ? 1 : -1;
            var yd = dir.y >= 0 ? 1 : -1;
            var zd = dir.z >= 0 ? 1 : -1;

            var xi = xIndex + xd;
            var yi = yIndex + yd;
            var zi = zIndex + zd;

            var xDone = false;
            var yDone = false;
            var zDone = false;

            var selfPos = xSorted[xIndex].position;
            for (; !xDone || !yDone || !zDone;)
            {
                if (xi < 0 || xi >= maxAgents) xDone = true;
                if (yi < 0 || yi >= maxAgents) yDone = true;
                if (zi < 0 || zi >= maxAgents) zDone = true;

                if (!zDone)
                {
                    var data = zSorted[zi];
                    if (maxDist < data.position.z - selfPos.z) zDone = true;
                    else list.Add(zSorted[zi].index);
                    zi += zd;
                }

                if (!xDone)
                {
                    var data = xSorted[xi];
                    if (maxDist < data.position.x - selfPos.x) xDone = true;
                    else list.Add(xSorted[xi].index);
                    xi += xd;
                }

                if (!yDone)
                {
                    var data = ySorted[yi];
                    if (maxDist < data.position.y - selfPos.y) yDone = true;
                    else list.Add(ySorted[yi].index);
                    yi += yd;
                }

                if (list.Count > maxCount) break;
            }
        }

        #region Comparer

        struct XComparer : IComparer<AgentPosition>
        {
            public int Compare(AgentPosition x, AgentPosition y) => x.position.x.CompareTo(y.position.x);
        }

        struct YComparer : IComparer<AgentPosition>
        {
            public int Compare(AgentPosition x, AgentPosition y) => x.position.y.CompareTo(y.position.y);
        }

        struct ZComparer : IComparer<AgentPosition>
        {
            public int Compare(AgentPosition x, AgentPosition y) => x.position.z.CompareTo(y.position.z);
        }

        #endregion
    }
}