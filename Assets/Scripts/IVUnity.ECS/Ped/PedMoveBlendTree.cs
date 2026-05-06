using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS.Ped
{
    public class PedMoveBlendTree
    {
        public struct BlendEntry
        {
            public int ClipIndex;
            public float Weight;
        }

        private int _idleClip = -1;
        private int _walkClip = -1;
        private int _runClip = -1;
        private int _sprintClip = -1;

        private BlendEntry[] _output = new BlendEntry[4];
        private int _outputCount;

        private float _currentMBR;

        private const float MBR_ACCELERATION = 4.0f;
        private const float MBR_DECELERATION = 2.0f;

        // Idle bone 0 orientation differs from locomotion clips by this amount
        private const float IDLE_FACING_OFFSET = 90f;

        public int OutputCount => _outputCount;
        public BlendEntry GetOutput(int i) => _output[i];
        public float CurrentMBR => _currentMBR;
        public int IdleClipIndex => _idleClip;
        public float IdleFacingOffset => IDLE_FACING_OFFSET;

        public void Configure(Dictionary<string, int> clipsByName)
        {
            _idleClip = Try(clipsByName, "idle");
            _walkClip = Try(clipsByName, "walk");
            _runClip = Try(clipsByName, "run");
            _sprintClip = Try(clipsByName, "sprint");
            _currentMBR = 0f;
        }

        public void Update(float desiredSpeed, float directionAngle, float headingRate = 0f, float dt = 0f)
        {
            if (dt <= 0f) dt = Time.deltaTime;
            _outputCount = 0;

            // MBR interpolation
            float desiredMBR = math.clamp(desiredSpeed, 0f, 3f);

            if (desiredMBR > _currentMBR)
            {
                _currentMBR += MBR_ACCELERATION * dt;
                _currentMBR = math.min(_currentMBR, desiredMBR);
            }
            else if (desiredMBR < _currentMBR)
            {
                _currentMBR -= MBR_DECELERATION * dt;
                _currentMBR = math.max(_currentMBR, math.max(desiredMBR, 0f));
            }

            // Blend weights from smoothed MBR
            float speed = math.clamp(_currentMBR, 0f, 3f);
            float idleW = math.saturate(1f - speed);
            float walkW, runW, sprintW;

            if (speed < 1f)
            {
                walkW = speed;
                runW = 0f;
                sprintW = 0f;
            }
            else if (speed < 2f)
            {
                walkW = 1f - (speed - 1f);
                runW = speed - 1f;
                sprintW = 0f;
            }
            else
            {
                walkW = 0f;
                runW = 1f - math.saturate(speed - 2f);
                sprintW = math.saturate(speed - 2f);
            }

            if (idleW > 0.001f && _idleClip >= 0)
                AddOutput(_idleClip, idleW);
            if (walkW > 0.001f && _walkClip >= 0)
                AddOutput(_walkClip, walkW);
            if (runW > 0.001f && _runClip >= 0)
                AddOutput(_runClip, runW);
            if (sprintW > 0.001f && _sprintClip >= 0)
                AddOutput(_sprintClip, sprintW);
        }

        private void AddOutput(int clip, float weight)
        {
            if (clip < 0 || _outputCount >= _output.Length) return;
            for (int i = 0; i < _outputCount; i++)
            {
                if (_output[i].ClipIndex == clip)
                {
                    _output[i].Weight += weight;
                    return;
                }
            }
            _output[_outputCount++] = new BlendEntry { ClipIndex = clip, Weight = weight };
        }

        static int Try(Dictionary<string, int> d, string k)
        {
            return d != null && d.TryGetValue(k, out int v) ? v : -1;
        }
    }
}
