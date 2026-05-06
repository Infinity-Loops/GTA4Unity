using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace IVUnity.ECS.GameMode
{
    public struct PedCharacterUpdateContext
    {
        public void OnSystemCreate(ref SystemState state) { }
        public void OnSystemUpdate(ref SystemState state) { }
    }

    public struct PedCharacterProcessor : IKinematicCharacterProcessor<PedCharacterUpdateContext>
    {
        public KinematicCharacterDataAccess CharacterDataAccess;
        public RefRW<PedCharacterComponent> CharacterComponent;
        public RefRW<PedCharacterControl> CharacterControl;

        public void PhysicsUpdate(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var cc = ref CharacterComponent.ValueRW;
            ref var body = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref var pos = ref CharacterDataAccess.LocalTransform.ValueRW.Position;

            KinematicCharacterUtilities.Update_Initialize(
                in this, ref context, ref baseContext,
                ref body,
                CharacterDataAccess.CharacterHitsBuffer,
                CharacterDataAccess.DeferredImpulsesBuffer,
                CharacterDataAccess.VelocityProjectionHits,
                baseContext.Time.DeltaTime);

            KinematicCharacterUtilities.Update_ParentMovement(
                in this, ref context, ref baseContext,
                CharacterDataAccess.CharacterEntity,
                ref body,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO,
                ref pos,
                body.WasGroundedBeforeCharacterUpdate);

            KinematicCharacterUtilities.Update_Grounding(
                in this, ref context, ref baseContext,
                ref body,
                CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO,
                CharacterDataAccess.VelocityProjectionHits,
                CharacterDataAccess.CharacterHitsBuffer,
                ref pos);

            HandleVelocityControl(ref context, ref baseContext);

            KinematicCharacterUtilities.Update_PreventGroundingFromFutureSlopeChange(
                in this, ref context, ref baseContext,
                CharacterDataAccess.CharacterEntity,
                ref body,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                in cc.StepAndSlopeHandling);

            KinematicCharacterUtilities.Update_GroundPushing(
                in this, ref context, ref baseContext,
                ref body,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO,
                CharacterDataAccess.DeferredImpulsesBuffer,
                cc.Gravity);

            KinematicCharacterUtilities.Update_MovementAndDecollisions(
                in this, ref context, ref baseContext,
                CharacterDataAccess.CharacterEntity,
                ref body,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO,
                CharacterDataAccess.VelocityProjectionHits,
                CharacterDataAccess.CharacterHitsBuffer,
                CharacterDataAccess.DeferredImpulsesBuffer,
                ref pos);

            KinematicCharacterUtilities.Update_MovingPlatformDetection(
                ref baseContext, ref body);

            KinematicCharacterUtilities.Update_ParentMomentum(
                ref baseContext, ref body,
                CharacterDataAccess.LocalTransform.ValueRO.Position);

            KinematicCharacterUtilities.Update_ProcessStatefulCharacterHits(
                CharacterDataAccess.CharacterHitsBuffer,
                CharacterDataAccess.StatefulHitsBuffer);
        }

        private void HandleVelocityControl(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            float dt = baseContext.Time.DeltaTime;
            ref var body = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref var cc = ref CharacterComponent.ValueRW;
            ref var ctrl = ref CharacterControl.ValueRW;

            if (body.ParentEntity != Entity.Null)
            {
                ctrl.MoveVector = math.rotate(body.RotationFromParent, ctrl.MoveVector);
                body.RelativeVelocity = math.rotate(body.RotationFromParent, body.RelativeVelocity);
            }

            if (body.IsGrounded)
            {
                float speed = ctrl.MoveSpeed > 0f ? ctrl.MoveSpeed : cc.GroundMaxSpeed;
                float3 targetVel = ctrl.MoveVector * speed;
                CharacterControlUtilities.StandardGroundMove_Interpolated(
                    ref body.RelativeVelocity, targetVel,
                    cc.GroundedMovementSharpness, dt,
                    body.GroundingUp, body.GroundHit.Normal);

                if (ctrl.Jump)
                    CharacterControlUtilities.StandardJump(ref body, body.GroundingUp * cc.JumpSpeed, true, body.GroundingUp);
            }
            else
            {
                float3 airAccel = ctrl.MoveVector * cc.AirAcceleration;
                if (math.lengthsq(airAccel) > 0f)
                {
                    float3 prevVel = body.RelativeVelocity;
                    CharacterControlUtilities.StandardAirMove(
                        ref body.RelativeVelocity, airAccel,
                        cc.AirMaxSpeed, body.GroundingUp, dt, false);

                    if (cc.StepAndSlopeHandling.PreventGroundingWhenMovingTowardsNoGrounding
                        && KinematicCharacterUtilities.MovementWouldHitNonGroundedObstruction(
                            in this, ref context, ref baseContext,
                            CharacterDataAccess.CharacterProperties.ValueRO,
                            CharacterDataAccess.LocalTransform.ValueRO,
                            CharacterDataAccess.CharacterEntity,
                            CharacterDataAccess.PhysicsCollider.ValueRO,
                            body.RelativeVelocity * dt,
                            out _))
                    {
                        body.RelativeVelocity = prevVel;
                    }
                }

                CharacterControlUtilities.AccelerateVelocity(ref body.RelativeVelocity, cc.Gravity, dt);
                CharacterControlUtilities.ApplyDragToVelocity(ref body.RelativeVelocity, dt, cc.AirDrag);
            }
        }

        public void VariableUpdate(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var body = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref var cc = ref CharacterComponent.ValueRW;
            ref var ctrl = ref CharacterControl.ValueRW;
            ref var rot = ref CharacterDataAccess.LocalTransform.ValueRW.Rotation;

            KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(
                ref rot, body.RotationFromParent,
                baseContext.Time.DeltaTime, body.LastPhysicsUpdateDeltaTime);

            // Rotate toward move direction (inverted for bone 0 model-space emulation)
            // Isolate XZ for facing — Y would introduce roll
            float3 facingDir = new float3(-ctrl.MoveVector.x, 0f, -ctrl.MoveVector.z);
            if (math.lengthsq(facingDir) > 0f)
            {
                CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(
                    ref rot, baseContext.Time.DeltaTime,
                    math.normalizesafe(facingDir),
                    MathUtilities.GetUpFromRotation(rot),
                    cc.RotationSharpness);
            }
        }

        #region IKinematicCharacterProcessor callbacks

        public void UpdateGroundingUp(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var body = ref CharacterDataAccess.CharacterBody.ValueRW;
            KinematicCharacterUtilities.Default_UpdateGroundingUp(ref body, CharacterDataAccess.LocalTransform.ValueRO.Rotation);
        }

        public bool CanCollideWithHit(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in BasicHit hit)
        {
            return PhysicsUtilities.IsCollidable(hit.Material);
        }

        public bool IsGroundedOnHit(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in BasicHit hit, int groundingEvaluationType)
        {
            return KinematicCharacterUtilities.Default_IsGroundedOnHit(
                in this, ref context, ref baseContext,
                CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.CharacterBody.ValueRO,
                CharacterDataAccess.CharacterProperties.ValueRO,
                in hit,
                in CharacterComponent.ValueRO.StepAndSlopeHandling,
                groundingEvaluationType);
        }

        public void OnMovementHit(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit, ref float3 remainingMovementDirection, ref float remainingMovementLength,
            float3 originalVelocityDirection, float hitDistance)
        {
            ref var body = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref var pos = ref CharacterDataAccess.LocalTransform.ValueRW.Position;
            var cc = CharacterComponent.ValueRO;

            KinematicCharacterUtilities.Default_OnMovementHit(
                in this, ref context, ref baseContext,
                ref body,
                CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO,
                ref pos,
                CharacterDataAccess.VelocityProjectionHits,
                ref hit,
                ref remainingMovementDirection,
                ref remainingMovementLength,
                originalVelocityDirection,
                hitDistance,
                cc.StepAndSlopeHandling.StepHandling,
                cc.StepAndSlopeHandling.MaxStepHeight,
                cc.StepAndSlopeHandling.CharacterWidthForStepGroundingCheck);
        }

        public void OverrideDynamicHitMasses(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass, ref PhysicsMass otherMass, BasicHit hit) { }

        public void ProjectVelocityOnHits(ref PedCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity, ref bool characterIsGrounded, ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits, float3 originalVelocityDirection)
        {
            KinematicCharacterUtilities.Default_ProjectVelocityOnHits(
                ref velocity, ref characterIsGrounded, ref characterGroundHit,
                in velocityProjectionHits, originalVelocityDirection,
                CharacterComponent.ValueRO.StepAndSlopeHandling.ConstrainVelocityToGroundPlane,
                in CharacterDataAccess.CharacterBody.ValueRO);
        }

        #endregion
    }
}
