using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Mewlist.Boid
{
    public struct AgentData : IComponentData
    {
        public float3 Velocity;
    }

    [Serializable]
    public struct SharedAgentData : ISharedComponentData
    {
        public SimulationTime SimulationTime;
        public float2         VelocityRange;
        public float3         Follow;
        public float          CohesionFactor;
        public float          SeparationFactor;
        public float          AlignmentFactor;
        public float          PreyFactor;
        public float          MaxCohesionDistance;
        public float          MaxAlignmentDistance;
        public int            MaxNeighborCount;
    }

    public struct AgentCollection
    {
        public readonly   int                           Length;
        public            ComponentDataArray<Position>  positions;
        public            ComponentDataArray<Rotation>  rotations;
        public            ComponentDataArray<AgentData> agents;
        [ReadOnly] public EntityArray                   entities;
    }
}