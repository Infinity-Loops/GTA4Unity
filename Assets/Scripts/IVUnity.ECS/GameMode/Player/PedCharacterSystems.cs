using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct PedCharacterPhysicsUpdateSystem : ISystem
    {
        EntityQuery _query;
        PedCharacterUpdateContext _context;
        KinematicCharacterUpdateContext _baseContext;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<PedCharacterComponent, PedCharacterControl>()
                .Build(ref state);

            _context = new PedCharacterUpdateContext();
            _context.OnSystemCreate(ref state);
            _baseContext = new KinematicCharacterUpdateContext();
            _baseContext.OnSystemCreate(ref state);

            state.RequireForUpdate(_query);
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _context.OnSystemUpdate(ref state);
            _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

            new PedCharacterPhysicsUpdateJob
            {
                Context = _context,
                BaseContext = _baseContext,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct PedCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public PedCharacterUpdateContext Context;
            public KinematicCharacterUpdateContext BaseContext;

            public void Execute(
                Entity entity,
                RefRW<LocalTransform> localTransform,
                RefRW<KinematicCharacterProperties> characterProperties,
                RefRW<KinematicCharacterBody> characterBody,
                RefRW<PhysicsCollider> physicsCollider,
                RefRW<PedCharacterComponent> characterComponent,
                RefRW<PedCharacterControl> characterControl,
                DynamicBuffer<KinematicCharacterHit> hitsBuffer,
                DynamicBuffer<StatefulKinematicCharacterHit> statefulHitsBuffer,
                DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
                DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
            {
                var processor = new PedCharacterProcessor
                {
                    CharacterDataAccess = new KinematicCharacterDataAccess(
                        entity, localTransform, characterProperties, characterBody, physicsCollider,
                        hitsBuffer, statefulHitsBuffer, deferredImpulsesBuffer, velocityProjectionHits),
                    CharacterComponent = characterComponent,
                    CharacterControl = characterControl,
                };
                processor.PhysicsUpdate(ref Context, ref BaseContext);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BaseContext.EnsureCreationOfTmpCollections();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted) { }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PedPlayerVariableStepControlSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct PedCharacterVariableUpdateSystem : ISystem
    {
        EntityQuery _query;
        PedCharacterUpdateContext _context;
        KinematicCharacterUpdateContext _baseContext;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<PedCharacterComponent, PedCharacterControl>()
                .Build(ref state);

            _context = new PedCharacterUpdateContext();
            _context.OnSystemCreate(ref state);
            _baseContext = new KinematicCharacterUpdateContext();
            _baseContext.OnSystemCreate(ref state);

            state.RequireForUpdate(_query);
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _context.OnSystemUpdate(ref state);
            _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

            new PedCharacterVariableUpdateJob
            {
                Context = _context,
                BaseContext = _baseContext,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct PedCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public PedCharacterUpdateContext Context;
            public KinematicCharacterUpdateContext BaseContext;

            public void Execute(
                Entity entity,
                RefRW<LocalTransform> localTransform,
                RefRW<KinematicCharacterProperties> characterProperties,
                RefRW<KinematicCharacterBody> characterBody,
                RefRW<PhysicsCollider> physicsCollider,
                RefRW<PedCharacterComponent> characterComponent,
                RefRW<PedCharacterControl> characterControl,
                DynamicBuffer<KinematicCharacterHit> hitsBuffer,
                DynamicBuffer<StatefulKinematicCharacterHit> statefulHitsBuffer,
                DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
                DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
            {
                var processor = new PedCharacterProcessor
                {
                    CharacterDataAccess = new KinematicCharacterDataAccess(
                        entity, localTransform, characterProperties, characterBody, physicsCollider,
                        hitsBuffer, statefulHitsBuffer, deferredImpulsesBuffer, velocityProjectionHits),
                    CharacterComponent = characterComponent,
                    CharacterControl = characterControl,
                };
                processor.VariableUpdate(ref Context, ref BaseContext);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BaseContext.EnsureCreationOfTmpCollections();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted) { }
        }
    }
}
