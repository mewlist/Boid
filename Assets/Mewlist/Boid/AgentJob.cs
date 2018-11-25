using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Mewlist.Boid
{
    internal struct AgentJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] private ComponentDataArray<Position>  positions;
        private                                       ComponentDataArray<Rotation>  rotations;
        [NativeDisableParallelForRestriction] private ComponentDataArray<AgentData> agents;

        private float           timeDelta;
        private Cluster         cluster;
        private SharedAgentData agentData;

        public AgentJob(int count)
        {
            timeDelta = Time.deltaTime;
            cluster   = new Cluster(count);
            positions = new ComponentDataArray<Position>();
            rotations = new ComponentDataArray<Rotation>();
            agents    = new ComponentDataArray<AgentData>();
            agentData = default(SharedAgentData);
        }

        public void Release()
        {
            cluster.Release();
        }

        public void Execute(int i)
        {
            Process(i);
        }

        public void Update(AgentCollection agentCollection, SharedAgentData sharedAgentData)
        {
            cluster.SetAgentPositions(agentCollection.Positions);

            positions = agentCollection.Positions;
            rotations = agentCollection.Potations;
            agents    = agentCollection.Agents;
            agentData = sharedAgentData;

            switch (agentData.SimulationTime)
            {
                case SimulationTime.TimeDelta:
                    timeDelta = Time.deltaTime;
                    break;
                case SimulationTime.Constant10ms:
                    timeDelta = 0.01f;
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void Process(int i)
        {
            var position      = positions[i];
            var rotation      = rotations[i];
            var agent         = agents[i];
            var force         = float3.zero;
            var neighborsHash = new HashSet<int>();

            cluster.SearchNeighbors(i, agent.Velocity, ref neighborsHash, 5f, agentData.MaxNeighborCount);

            var neighborsList = neighborsHash.ToArray();
            var distances     = new NativeArray<float>(neighborsList.Length, Allocator.Temp);
            var dots          = new NativeArray<float>(neighborsList.Length, Allocator.Temp);

            for (var j = 0; j < neighborsList.Length; j++)
            {
                var neighborIndex = neighborsList[j];
                var direction     = positions[neighborIndex].Value - positions[i].Value;

                distances[j] = Vector3.Magnitude(direction);
                dots[j]      = Vector3.Dot(agents[i].Velocity, direction);
            }

            force += Cohesion(i, neighborsList, distances, dots);
            force += Separation(i, neighborsList, distances, dots);
            force += Alignment(i, neighborsList, distances, dots);
            force += Prey(i, neighborsList, distances, dots);

            var velocity  = agent.Velocity + force * timeDelta;
            var hVelocity = new Vector2(velocity.x, velocity.z).magnitude;

            velocity.y = Mathf.Clamp(velocity.y, -0.8f * hVelocity, 0.8f * hVelocity);

            var normalizedVelocity = Vector3.Normalize(velocity);
            var velocityMagnitude  = Vector3.Magnitude(velocity);

            if (velocityMagnitude < agentData.VelocityRange.x)
                agent.Velocity = normalizedVelocity * agentData.VelocityRange.x;
            else if (velocityMagnitude > agentData.VelocityRange.y)
                agent.Velocity = normalizedVelocity * agentData.VelocityRange.y;
            else
                agent.Velocity = normalizedVelocity * (velocityMagnitude + agentData.VelocityRange.y) / 2f;

            position.Value += agent.Velocity                          * timeDelta;
            rotation.Value =  Quaternion.LookRotation(agent.Velocity) * Quaternion.Euler(-90f, 0f, 0f);

            positions[i] = position;
            rotations[i] = rotation;
            agents[i]    = agent;

            distances.Dispose();
            dots.Dispose();
        }

        private float3 Cohesion(
            int                index,
            int[]              neighbors,
            NativeArray<float> distances,
            NativeArray<float> dots)
        {
            var position    = positions[index];
            var agent       = agents[index];
            var center      = float3.zero;
            var targetCount = 0;

            for (var i = 0; i < neighbors.Length; i++)
            {
                var neighborIndex = neighbors[i];
                if (index == neighborIndex) continue;
                if (distances[i] < agentData.MaxCohesionDistance && dots[i] > -1f)
                {
                    center += positions[neighborIndex].Value;
                    targetCount++;
                }
            }

            if (targetCount > 0) center /= targetCount;

            return targetCount > 0
                ? agentData.CohesionFactor * (center - position.Value)
                : 0;
        }

        private float3 Separation(
            int                index,
            int[]              neighbors,
            NativeArray<float> distances,
            NativeArray<float> dots)
        {
            var position             = positions[index];
            var agent                = agents[index];
            var separation           = float3.zero;
            var nearestNeighborIndex = -1;
            var minDist              = float.MaxValue;

            for (var i = 0; i < neighbors.Length; i++)
            {
                var neighborIndex = neighbors[i];
                if (index == neighborIndex) continue;
                if (distances[i] < agentData.SeparationFactor && distances[i] < minDist && dots[i] > 0f)
                {
                    minDist              = distances[i];
                    nearestNeighborIndex = i;
                }
            }

            if (nearestNeighborIndex != -1)
            {
                float3 dir = Vector3.Normalize(position.Value - positions[nearestNeighborIndex].Value);
                separation += Mathf.Pow(agentData.MaxCohesionDistance - minDist, 2f) * dir;
            }

            return agentData.SeparationFactor * separation;
        }

        private float3 Alignment(
            int                index,
            int[]              neighbors,
            NativeArray<float> distances,
            NativeArray<float> dots)
        {
            var position    = positions[index];
            var agent       = agents[index];
            var targetCount = 0;
            var alignment   = float3.zero;

            for (var i = 0; i < neighbors.Length; i++)
            {
                var neighborIndex = neighbors[i];
                if (index == neighborIndex) continue;
                if (distances[i] < agentData.MaxAlignmentDistance && dots[i] > 0.5f)
                {
                    alignment += agents[neighborIndex].Velocity;
                    targetCount++;
                }
            }

            if (targetCount > 0) alignment /= targetCount;

            return agentData.AlignmentFactor * alignment;
        }

        private float3 Prey(
            int                index,
            int[]              neighbors,
            NativeArray<float> distances,
            NativeArray<float> dots)
        {
            var position = positions[index];
            var agent    = agents[index];

            var distanceToPrey = Vector3.Magnitude(agentData.Prey - position.Value);
            return agentData.PreyFactor * (agentData.Prey - position.Value) * distanceToPrey * 0.01f;
        }
    }
}