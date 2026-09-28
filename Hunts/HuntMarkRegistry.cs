using System.Collections.Generic;

namespace AsMobPlate.Hunts;

public sealed class HuntMarkRegistry
{
    // BNpcNameID values verified in ACT object-add records (2026-09-24/26).
    public static readonly HashSet<uint> MinionNameIds = new() { 0x22D4, 0x2978, 0x345F };
    public static readonly HashSet<uint> SsNameIds = new() { 0x22D3, 0x2977, 0x345E };

    public static bool IsSsTerritory(uint territory) => territory is >= 813 and <= 818
        or >= 956 and <= 961 or >= 1187 and <= 1192;

    public static uint SsNameId(uint territory) => territory switch
    {
        >= 813 and <= 818 => 0x22D3,
        >= 956 and <= 961 => 0x2977,
        >= 1187 and <= 1192 => 0x345E,
        _ => 0,
    };

    public static readonly HashSet<uint> ARankNameIds = new()
    {
        2936, 2937, 2938, 2939, 2940, 2941, 2942, 2943, 2944, 2945, 2946, 2947, 2948, 2949, 2950, 2951,
        2952, 4362, 4363, 4364, 4365, 4366, 4367, 4368, 4369, 4370, 4371, 4372, 4373, 5990, 5991, 5992,
        5993, 5994, 5995, 5996, 5997, 5998, 5999, 6000, 6001, 8654, 8655, 8891, 8892, 8896, 8897, 8901,
        8902, 8906, 8907, 8911, 8912, 10623, 10624, 10625, 10626, 10627, 10628, 10629, 10630, 10631,
        10632, 10633, 10634, 12692, 12753, 13157, 13158, 13361, 13362, 13400, 13401, 13435, 13436,
        13442, 13443,
    };

    public static readonly HashSet<uint> SRankNameIds = new()
    {
        2953, 2954, 2955, 2956, 2957, 2958, 2959, 2960, 2961, 2962, 2963, 2964, 2965, 2966, 2967, 2968,
        2969, 4374, 4375, 4376, 4377, 4378, 4380, 5984, 5985, 5986, 5987, 5988, 5989, 8653, 8890, 8895,
        8900, 8905, 8910, 10617, 10618, 10619, 10620, 10621, 10622, 12754, 13156, 13360, 13399, 13437,
        13444,
    };

    public HuntRank GetRank(uint nameId)
    {
        if (MinionNameIds.Contains(nameId))
            return HuntRank.Minion;
        if (SsNameIds.Contains(nameId))
            return HuntRank.SS;
        if (ARankNameIds.Contains(nameId))
            return HuntRank.A;

        if (SRankNameIds.Contains(nameId))
            return HuntRank.S;

        return HuntRank.None;
    }
}
