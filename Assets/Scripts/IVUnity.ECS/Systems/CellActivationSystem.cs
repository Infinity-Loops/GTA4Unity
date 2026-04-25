using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.ECS
{
    /// <summary>
    /// Chunk-level streaming decision. Iterates per (CellIndex, StreamingState) archetype
    /// chunk, computes cell-to-focus distance, and transitions the whole chunk via
    /// SetSharedComponent when it crosses the stream in/out radius.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(FocusPointSyncSystem))]
    public partial class CellActivationSystem : SystemBase
    {
        private EntityQuery worldInstanceQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
            RequireForUpdate<StreamingConfig>();

            worldInstanceQuery = GetEntityQuery(
                ComponentType.ReadOnly<WorldInstanceTag>(),
                ComponentType.ReadOnly<CellIndex>(),
                ComponentType.ReadOnly<StreamingState>());
        }

        protected override void OnUpdate()
        {
            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            var focus = SystemAPI.GetSingleton<FocusPointData>();

            var cellIndexHandle = GetSharedComponentTypeHandle<CellIndex>();
            var streamStateHandle = GetSharedComponentTypeHandle<StreamingState>();
            using var chunks = worldInstanceQuery.ToArchetypeChunkArray(Allocator.Temp);

            // Shared-component chunking: each chunk has exactly one (cell, state) combo.
            // Collect distinct pairs first, issue the state transitions in a second pass
            // so we don't mutate archetype memory mid-iteration.
            var groups = new HashSet<(int2 cell, StreamingStateValue state)>();
            foreach (var chunk in chunks)
            {
                var cell = chunk.GetSharedComponent(cellIndexHandle).Cell;
                var state = chunk.GetSharedComponent(streamStateHandle).Value;
                groups.Add((cell, state));
            }

            foreach (var key in groups)
            {
                var cell = key.cell;
                var state = key.state;
                float dist = CellMath.DistanceToCellCenterXZ(cell, cfg.CellSize, focus.Position);

                StreamingStateValue? target = null;
                if (state == StreamingStateValue.Dormant && dist <= cfg.StreamInDistance)
                    target = StreamingStateValue.Pending;
                else if (state == StreamingStateValue.Loaded && dist >= cfg.StreamOutDistance)
                    target = StreamingStateValue.Unloading;
                else if (state == StreamingStateValue.Pending && dist >= cfg.StreamOutDistance)
                    target = StreamingStateValue.Dormant;

                if (!target.HasValue) continue;

                // Both shared components must be in WithAll for SetSharedComponentFilter to match.
                var q = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<WorldInstanceTag, CellIndex, StreamingState>()
                    .Build(EntityManager);
                q.SetSharedComponentFilter(
                    new CellIndex { Cell = cell },
                    new StreamingState { Value = state });

                if (!q.IsEmpty)
                {
                    EntityManager.SetSharedComponent(q, new StreamingState { Value = target.Value });
                }
            }
        }
    }
}
