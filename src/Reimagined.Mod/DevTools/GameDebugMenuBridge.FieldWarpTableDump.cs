#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            public static void DumpFieldWarpTableAndContext()
            {
                string dumpPath = MakeDumpPath("field_warp_table", "txt");
                bool ok = DumpFieldWarpTableAndContext(dumpPath, out var summary);

                if (ok)
                {
                    s_warpLastActionSummary = $"dumped field_warp_table -> {Path.GetFileName(dumpPath)} ({summary})";
                    MelonLogger.Msg($"[Reimagined] Field warp table dumped: {dumpPath} ({summary})");
                }
                else
                {
                    s_warpLastActionSummary = $"dump failed: {summary}";
                    MelonLogger.Warning($"[Reimagined] Field warp table dump failed: {summary}");
                }
            }

            public static bool DumpFieldWarpTableAndContext(string dumpPath, out string summary)
            {
                summary = "";

                try
                {
                    var lines = new List<string>(capacity: 512);
                    lines.Add("# SMT3HD_Reimagined · Field Warp Table Dump");
                    lines.Add($"# time={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    lines.Add("");

                    // Include current field context (if available).
                    if (TryGetFieldMapHudLines(dumpMode: true, out var ctxLines) && ctxLines.Length > 0)
                    {
                        lines.Add("[FieldContext]");
                        for (int i = 0; i < ctxLines.Length; i++)
                            lines.Add("  " + ctxLines[i]);
                        lines.Add("");
                    }

                    if (!TryGetDoorBuff(out var doorBuff, out int doorCount) || doorBuff == null || doorCount <= 0)
                    {
                        lines.Add("[DoorBuff]");
                        lines.Add("  <door_buff not available>");
                        File.WriteAllLines(dumpPath, lines, Encoding.UTF8);
                        summary = "door_buff not available";
                        return false;
                    }

                    lines.Add("[DoorBuff]");
                    lines.Add($"  doorCount={doorCount}");
                    lines.Add("");

                    // Dump table.
                    lines.Add("[DoorTable]");
	                lines.Add("# idx mt wt wt2 attr fmode flag u01 u03 u04 u05 u02 u06 u07 u08 u09 u10 pos cm ct cam bgm foot aflag after");

                    int okCount = 0;
                    for (int i = 0; i < doorCount; i++)
                    {
                        if (!TryGetArrayElement(doorBuff, i, out var doorObj) || doorObj == null)
                        {
                            lines.Add($"{i,4} <null>");
                            continue;
                        }

                        byte movetype = GetByte(doorObj, "movetype", 0);
                        byte warptype = GetByte(doorObj, "warptype", 0);
                        byte warptype2 = GetByte(doorObj, "warptype2", 0);
                        ushort warpAttr = GetUShort(doorObj, "warp_attr", 0);
                        ushort flagmode = GetUShort(doorObj, "flagmode", 0);
                        ushort flag = GetUShort(doorObj, "flag", 0);

	                    // These unions are important for correlating door hits (fldEveHit.OldHitInf)
	                    // and various warp executors. Keep them as ints for grep-friendly dumps.
	                    int u01 = GetInt(doorObj, "union01", 0);
	                    int u03 = GetInt(doorObj, "union03", 0);
	                    int u04 = GetInt(doorObj, "union04", 0);
	                    int u05 = GetInt(doorObj, "union05", 0);

	                    string union02 = San(GetMaybeFixedCharString(doorObj, "union02", 128));
	                    string union06 = San(GetMaybeFixedCharString(doorObj, "union06", 128));
	                    string union07 = San(GetMaybeFixedCharString(doorObj, "union07", 128));
	                    int u08 = GetInt(doorObj, "union08", 0);
	                    int u09 = GetInt(doorObj, "union09", 0);
	                    int u10 = GetInt(doorObj, "union10", 0);
	                    string pos = San(GetMaybeFixedCharString(doorObj, "warp_pos", 128));
	                    int camMode = GetInt(doorObj, "warp_cammode", 0);
	                    int camTbl = GetInt(doorObj, "warp_camtbl", 0);
	                    string cam = San(GetMaybeFixedCharString(doorObj, "warp_camname", 128));
	                    int bgm = GetInt(doorObj, "warp_bgm", 0);
	                    int foot = GetInt(doorObj, "warp_foot", 0);
	                    int afterFlag = GetInt(doorObj, "after_flag", 0);
	                    string after = San(GetMaybeFixedCharString(doorObj, "after_scr", 128));

                        // Keep the primary line grep-friendly and stable.
	                    lines.Add(string.Format(CultureInfo.InvariantCulture,
	                        "{0,4} mt={1} wt={2} wt2={3} attr={4} fmode={5} flag={6} u01={7} u03={8} u04={9} u05={10} u02=\"{11}\" u06=\"{12}\" u07=\"{13}\" u08={14} u09={15} u10={16} pos=\"{17}\" cm={18} ct={19} cam=\"{20}\" bgm={21} foot={22} aflag={23} after=\"{24}\"",
	                        i, movetype, warptype, warptype2, warpAttr, flagmode, flag, u01, u03, u04, u05, union02, union06, union07, u08, u09, u10,
	                        pos,
	                        camMode, camTbl,
	                        cam,
	                        bgm,
	                        foot,
	                        afterFlag,
	                        after));

                        okCount++;
                    }

                    File.WriteAllLines(dumpPath, lines, Encoding.UTF8);
                    summary = $"doorCount={doorCount} dumped={okCount}";
                    return true;
                }
                catch (Exception ex)
                {
                    summary = $"exception: {ex.GetType().Name}: {ex.Message}";
                    MelonLogger.Warning($"[Reimagined] DumpFieldWarpTableAndContext failed: {summary}");
                    return false;
                }
            }

            private static byte GetByte(object obj, string memberName, byte fallback)
            {
                try
                {
                    object? v = GetMember(obj, memberName);
                    if (v == null)
                        return fallback;

                    if (v is byte b) return b;
                    if (v is sbyte sb) return unchecked((byte)sb);
                    if (v is short s) return unchecked((byte)s);
                    if (v is int i) return unchecked((byte)i);

                    return Convert.ToByte(v, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return fallback;
                }
            }

            private static ushort GetUShort(object obj, string memberName, ushort fallback)
            {
                try
                {
                    object? v = GetMember(obj, memberName);
                    if (v == null)
                        return fallback;

                    if (v is ushort us) return us;
                    if (v is short s) return unchecked((ushort)s);
                    if (v is int i) return unchecked((ushort)i);
                    if (v is uint ui) return unchecked((ushort)ui);

                    return Convert.ToUInt16(v, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return fallback;
                }
            }
        }
    }
}
