﻿using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mewlist.Boid
{
    public enum SimulationTime
    {
        TimeDelta,
        Constant10ms,
    }

    public class BoidManager : MonoBehaviour
    {
        // mesh to render
        [SerializeField]                   private Mesh     mesh;
        [SerializeField]                   private Material material;
        [Range(0.1f, 5f)] [SerializeField] private float    meshScale;

        // min-max velocity of agent
        [SerializeField] private Vector2 velocityRange;

        // cohesion
        [Range(0f, 5f)] [SerializeField]  private float cohesionFactor;
        [Range(0f, 20f)] [SerializeField] private float maxCohesionDistance;

        // separation
        [Range(0f, 20f)] [SerializeField] private float separationFactor;

        // alignment
        [Range(0f, 5f)] [SerializeField]  private float alignmentFactor;
        [Range(0f, 20f)] [SerializeField] private float maxAlignmentDistance;

        // prey (position of this game object)
        [Range(0f, 1f)] [SerializeField] private float preyFactor;

        // neighbor count to process
        [Range(1, 100)] [SerializeField] private int maxNeighborCount;

        // agent count
        [Range(1, 10000)] [SerializeField] private int agentCount;

        // simulation time
        [SerializeField] private SimulationTime simulationTime;


        private EntityManager   entityManager;
        private EntityArchetype entityArchetype;

        private void Start()
        {
            // get EntityManager
            var world = World.Active;
            entityManager = world.GetOrCreateManager<EntityManager>();

            // create ArchType
            entityArchetype = entityManager.CreateArchetype(
                typeof(Position),
                typeof(AgentData),
                typeof(Scale),
                typeof(MeshInstanceRenderer),
                typeof(SharedAgentData));

            // workaround for UniRx
            /*
            var playerLoop = ScriptBehaviourUpdateOrder.CurrentPlayerLoop;
            PlayerLoopHelper.Initialize(ref playerLoop);
            */

            AdjustEntityCount();
        }

        private void Update()
        {
        }

        private void AdjustEntityCount()
        {
            var allEntities = entityManager.GetAllEntities();
            var entityCount = allEntities.Length;

            var toRemove = Mathf.Max(0, entityCount - agentCount);
            var toCreate = Mathf.Max(0, agentCount  - entityCount);

            for (var i = 0; i < toRemove; i++)
                RemoveEntity(allEntities[i]);

            for (var i = 0; i < toCreate; i++)
                CreateEntity();
        }

        private void CreateEntity()
        {
            Entity entity = entityManager.CreateEntity(entityArchetype);
            SetEntityData(entity);
        }

        private void RemoveEntity(Entity entity)
        {
            entityManager.DestroyEntity(entity);
        }

        private void SetEntityData(Entity entity)
        {
            var direction = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)).normalized;
            var velocity = Random.Range(velocityRange.x, velocityRange.y) * direction;
            var position = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f));

            entityManager.SetComponentData(entity, new Position() {Value = position});
            entityManager.SetComponentData(entity, new Scale() {Value    = meshScale * Vector3.one});
            entityManager.SetComponentData(entity, new AgentData()
            {
                Velocity = velocity,
            });
            entityManager.SetSharedComponentData(entity, new MeshInstanceRenderer
            {
                mesh     = mesh,
                material = material,
            });

            entityManager.SetSharedComponentData(entity, new SharedAgentData
            {
                SimulationTime       = simulationTime,
                VelocityRange        = velocityRange,
                Follow               = transform.position,
                AlignmentFactor      = alignmentFactor,
                SeparationFactor     = separationFactor,
                CohesionFactor       = cohesionFactor,
                PreyFactor           = preyFactor,
                MaxCohesionDistance  = maxCohesionDistance,
                MaxAlignmentDistance = maxAlignmentDistance,
                MaxNeighborCount     = maxNeighborCount
            });
        }

        private void OnValidate()
        {
            if (entityManager == null) return;
            AdjustEntityCount();

            foreach (var entity in entityManager.GetAllEntities())
                SetEntityData(entity);
        }
    }
}