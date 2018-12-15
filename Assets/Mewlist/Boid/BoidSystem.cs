using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Mewlist.Boid
{
    public class BoidSystem : JobComponentSystem
    {
        [Inject] private AgentCollection agentCollection;

        private AgentJob       job;
        private int            agentLength = 0;
        private ComponentGroup group;

        private bool IsSetupRequired => agentCollection.Length != agentLength;

        private SharedAgentData SharedAgentData =>
            EntityManager.GetSharedComponentData<SharedAgentData>(agentCollection.Entities[0]);

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
        }

        protected override void OnDestroyManager()
        {
            Clear();
            base.OnDestroyManager();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (agentCollection.Length == 0) return inputDeps;
            if (IsSetupRequired) Setup();

            // update
            job.Update(agentCollection, SharedAgentData);

            // execute
            var jobHandle = job.Schedule(agentCollection.Length, 50, inputDeps);
            return jobHandle;
        }

        private void Setup()
        {
            Clear();

            job         = new AgentJob(agentCollection.Length);
            agentLength = agentCollection.Length;

            Debug.Log(string.Format("{0} agents are active.", agentLength));
        }

        private void Clear()
        {
            job.Release();
        }
    }
}