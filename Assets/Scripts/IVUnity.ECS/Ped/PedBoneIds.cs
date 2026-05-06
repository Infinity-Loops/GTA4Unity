namespace IVUnity.ECS.Ped
{
    public enum PedBoneId : ushort
    {
        Root = 0,
        Pelvis = 417,

        // Left leg
        L_Thigh = 418,
        L_Calf = 419,
        L_Foot = 420,
        L_Toe0 = 421,
        L_CalfRoll = 14512,

        // Right leg
        R_Thigh = 423,
        R_Calf = 424,
        R_Foot = 425,
        R_Toe0 = 1200,
        R_CalfRoll = 14768,

        // Spine
        Spine = 1202,
        Spine1 = 1203,
        Spine2 = 13984,
        Spine3 = 13985,
        Neck = 1204,
        NeckRoll = 14240,
        Head = 1205,

        // Left arm
        L_Clavicle = 1216,
        L_UpperArm = 1217,
        L_Forearm = 1218,
        L_Hand = 1219,
        L_ForeTwist = 14497,
        L_UpperArmRoll = 14496,
        L_ArmRoll = 15857,

        // Left fingers
        L_Finger0 = 13776,
        L_Finger01 = 13777,
        L_Finger02 = 13778,
        L_Finger1 = 13779,
        L_Finger11 = 13780,
        L_Finger12 = 13781,
        L_Finger2 = 13782,
        L_Finger21 = 13783,
        L_Finger22 = 13784,
        L_Finger3 = 13785,
        L_Finger31 = 13792,
        L_Finger32 = 13793,

        // Right arm
        R_Clavicle = 1223,
        R_UpperArm = 1224,
        R_Forearm = 1225,
        R_Hand = 1232,
        R_ForeTwist = 14753,
        R_UpperArmRoll = 14752,
        R_ArmRoll = 15873,

        // Right fingers
        R_Finger0 = 13744,
        R_Finger01 = 13745,
        R_Finger02 = 13746,
        R_Finger1 = 13747,
        R_Finger11 = 13748,
        R_Finger12 = 13749,
        R_Finger2 = 13750,
        R_Finger21 = 13751,
        R_Finger22 = 13752,
        R_Finger3 = 13753,
        R_Finger31 = 13760,
        R_Finger32 = 13761,

        // Face
        FB_C_Brow = 32660,
        FB_C_Cheeks = 32692,
        FB_L_Brow = 32666,
        FB_L_CrnMouth = 32677,
        FB_L_Eyeball = 32664,
        FB_L_Eyelid = 32665,
        FB_R_Brow = 32661,
        FB_R_CrnMouth = 32676,
        FB_R_Eyeball = 32663,
        FB_R_Eyelid = 32662,
        FB_C_Jaw = 32667,
        FB_R_LipLower = 32678,
        FB_L_LipLower = 32679,
        FB_L_LipUpper = 32669,
        FB_R_LipUpper = 32668,
    }
}
