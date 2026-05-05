using Unity.Mathematics;

namespace RageLib.Animation
{
    public struct BoneFrame
    {
        public ushort BoneId;
        public quaternion[] Rotations;
    }

    public class AnimationClip
    {
        public string Name;
        public float Duration;
        public int FrameCount;
        public float FrameRate;
        public BoneFrame[] BoneTracks;
        public float3[] MoverPositions;

        public static AnimationClip FromAnimationData(AnimationData data)
        {
            var clip = new AnimationClip
            {
                Name = data.Name,
                Duration = data.Duration,
                FrameCount = data.NumFrames,
                FrameRate = (data.NumFrames - 1) / data.Duration,
            };

            var boneList = new System.Collections.Generic.List<BoneFrame>();

            foreach (var track in data.Tracks)
            {
                if (track == null || track.Chunk == null) continue;
                var chunk = track.Chunk;

                if (track.TrackType == 1 && chunk.Channels != null && chunk.Channels.Length >= 4)
                {
                    // Bone rotation: 4 channels = quaternion XYZW
                    var ch0 = chunk.Channels[0];
                    var ch1 = chunk.Channels[1];
                    var ch2 = chunk.Channels[2];
                    var ch3 = chunk.Channels[3];
                    if (ch0 == null || ch1 == null || ch2 == null || ch3 == null) continue;

                    var xVals = ch0.GetValues(data.NumFrames);
                    var yVals = ch1.GetValues(data.NumFrames);
                    var zVals = ch2.GetValues(data.NumFrames);
                    var wVals = ch3.GetValues(data.NumFrames);

                    var rotations = new quaternion[data.NumFrames];
                    for (int f = 0; f < data.NumFrames; f++)
                    {
                        // Raw RAGE quaternion (same convention as skeleton bone.RotationQuaternion)
                        rotations[f] = new quaternion(xVals[f], yVals[f], zVals[f], wVals[f]);
                    }

                    boneList.Add(new BoneFrame
                    {
                        BoneId = track.BoneId,
                        Rotations = rotations,
                    });
                }
                else if (track.TrackType == 0 && chunk.Channels != null && chunk.Channels.Length >= 3)
                {
                    // Mover track: 3 channels = XYZ position
                    var ch0 = chunk.Channels[0];
                    var ch1 = chunk.Channels[1];
                    var ch2 = chunk.Channels[2];
                    if (ch0 == null || ch1 == null || ch2 == null) continue;

                    var xVals = ch0.GetValues(data.NumFrames);
                    var yVals = ch1.GetValues(data.NumFrames);
                    var zVals = ch2.GetValues(data.NumFrames);

                    clip.MoverPositions = new float3[data.NumFrames];
                    for (int f = 0; f < data.NumFrames; f++)
                        clip.MoverPositions[f] = new float3(xVals[f], yVals[f], zVals[f]);
                }
            }

            clip.BoneTracks = boneList.ToArray();
            return clip;
        }
    }
}
