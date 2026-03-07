#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            internal struct UnitResolveInfo
            {
                public int UnitworkIndex;
                public long Ptr;
                public int UnitId;
                public int Level;
                public int HP;
                public int MaxHP;
                public int MP;
                public int MaxMP;

                // Often zero in camp; appears to be used as a battle/runtime identity token in multiple systems.
                public int UniqueId;

                // Stable, diff-friendly code signatures extracted from datUnitWork.namecode/fullnamecode (char[8]).
                // These are far more useful than UniqueId in camp contexts.
                public uint NameCodeSig;
                public uint FullNameCodeSig;

                // Best-effort decoded strings (trimmed at '\0', sanitized for logs).
                public string NameCodeStr;
                public string FullNameCodeStr;

                // Back-compat: older dump code expected these names.
                public string NameCode { get { return NameCodeStr ?? string.Empty; } }
                public string FullNameCode { get { return FullNameCodeStr ?? string.Empty; } }

                public string NameTag;
            }

            private static Type? s_dds3GlobalWorkType;
            private static PropertyInfo? s_dds3GbwkProp;

            private static bool TryGetDds3GlobalWorkObject(out object? dds3GbwkObj)
            {
                dds3GbwkObj = null;
                try
                {
                    if (s_dds3GlobalWorkType == null)
                    {
                        s_dds3GlobalWorkType =
                            FindTypeInLoadedAssemblies("Il2Cpp.dds3GlobalWork") ??
                            FindTypeInLoadedAssemblies("dds3GlobalWork");

                        if (s_dds3GlobalWorkType != null)
                        {
                            s_dds3GbwkProp = s_dds3GlobalWorkType.GetProperty(
                                "DDS3_GBWK",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        }
                    }

                    if (s_dds3GbwkProp == null)
                        return false;

                    dds3GbwkObj = s_dds3GbwkProp.GetValue(null, null);
                    return dds3GbwkObj != null;
                }
                catch
                {
                    dds3GbwkObj = null;
                    return false;
                }
            }

            private static bool TryResolveUnitFromUnitworkIndex(object dds3GbwkObj, int uwIdx, out UnitResolveInfo info)
            {
                info = default;
                info.NameTag = "";

                if (uwIdx == int.MinValue || uwIdx < 0)
                    return false;

                try
                {
                    if (!TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) || unitworkArr == null)
                        return false;

                    if (!TryGetLengthOrCount(unitworkArr, out int uwLen, out _))
                        return false;

                    if (uwIdx < 0 || uwIdx >= uwLen)
                        return false;

                    object? unitObj = null;
                    try { unitObj = TryGetIndexValue(unitworkArr, uwIdx); } catch { unitObj = null; }

                    if (unitObj != null && TryFillUnitResolveInfo(unitObj, uwIdx, out info))
                        return true;
                }
                catch
                {
                    return false;
                }

                return false;
            }

            private static bool TryGetStocklistUnitworkIndex(object dds3GbwkObj, int stocklistIdx, out int uwIdx)
            {
                uwIdx = -1;

                if (stocklistIdx == int.MinValue || stocklistIdx < 0)
                    return false;

                try
                {
                    if (!TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) || unitworkArr == null)
                        return false;

                    if (!TryGetLengthOrCount(unitworkArr, out int uwLen, out _))
                        return false;

                    if (!TryGetIntArrayAt(dds3GbwkObj, "stocklist", stocklistIdx, out int idx))
                        return false;

                    if (idx < 0 || idx >= uwLen)
                        return false;

                    uwIdx = idx;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryResolveUnitFromStocklistIndex(object dds3GbwkObj, int stocklistIdx, out UnitResolveInfo info)
            {
                info = default;
                info.NameTag = "";

                if (!TryGetStocklistUnitworkIndex(dds3GbwkObj, stocklistIdx, out int uwIdx))
                    return false;

                return TryResolveUnitFromUnitworkIndex(dds3GbwkObj, uwIdx, out info);
            }


            private static bool TryResolveUnitFromCandidateIndex(object dds3GbwkObj, int candidateIndex, out UnitResolveInfo info)
            {
                info = default;
                info.NameTag = "";

                if (candidateIndex == int.MinValue || candidateIndex < 0)
                    return false;

                try
                {
                    if (!TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) || unitworkArr == null)
                        return false;

                    if (!TryGetLengthOrCount(unitworkArr, out int uwLen, out _))
                        return false;

                    // 1) Treat candidate as a direct unitwork index.
                    if (candidateIndex >= 0 && candidateIndex < uwLen)
                    {
                        object? unitObj = null;
                        try { unitObj = TryGetIndexValue(unitworkArr, candidateIndex); } catch { unitObj = null; }

                        if (unitObj != null && TryFillUnitResolveInfo(unitObj, candidateIndex, out info))
                            return true;
                    }

                    // 2) Treat candidate as an index into stocklist -> unitwork index.
                    if (TryGetIntArrayAt(dds3GbwkObj, "stocklist", candidateIndex, out int uwIdx))
                    {
                        if (uwIdx >= 0 && uwIdx < uwLen)
                        {
                            object? unitObj = null;
                            try { unitObj = TryGetIndexValue(unitworkArr, uwIdx); } catch { unitObj = null; }
                            if (unitObj != null && TryFillUnitResolveInfo(unitObj, uwIdx, out info))
                                return true;
                        }
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }

            private static bool TryFillUnitResolveInfo(object unitObj, int uwIdx, out UnitResolveInfo info)
            {
                info = default;
                info.UnitworkIndex = uwIdx;

                // Useful for correlating against cmpCalc selection pointers.
                try { info.Ptr = TryGetIl2CppPointer(unitObj).ToInt64(); } catch { info.Ptr = 0; }

                info.UnitId = TryReadInt32(unitObj, "id", -1);
                info.Level = TryReadInt32(unitObj, "level", -1);
                info.HP = TryReadInt32(unitObj, "hp", -1);
                info.MaxHP = TryReadInt32(unitObj, "maxhp", -1);
                info.MP = TryReadInt32(unitObj, "mp", -1);
                info.MaxMP = TryReadInt32(unitObj, "maxmp", -1);
                info.UniqueId = TryReadInt32(unitObj, "uniqueid", -1);

                info.NameCodeSig = 0;
                info.FullNameCodeSig = 0;
                info.NameCodeStr = "";
                info.FullNameCodeStr = "";
                if (TryReadCharCode8(unitObj, "namecode", out string ncStr, out uint ncSig))
                {
                    info.NameCodeStr = ncStr;
                    info.NameCodeSig = ncSig;
                }
                if (TryReadCharCode8(unitObj, "fullnamecode", out string fncStr, out uint fncSig))
                {
                    info.FullNameCodeStr = fncStr;
                    info.FullNameCodeSig = fncSig;
                }

                info.NameTag = "";
                if (info.UnitId >= 0 && TryGetDevilNameBestEffort(info.UnitId, out string nm))
                    info.NameTag = nm;

                // If we didn't get a devil-name, keep the struct but caller can decide if this is useful.
                return true;
            }

            
            private static bool TryReadCharCode8(object obj, string memberName, out string codeStr, out uint sig)
            {
                codeStr = "";
                sig = 0;

                try
                {
                    if (!TryGetInstanceMemberValue(obj, memberName, out object? raw) || raw == null)
                        return false;

                    if (raw is Il2CppStructArray<char> ca)
                    {
                        int n = Math.Min(8, ca.Length);
                        if (n <= 0) return false;

                        // Build a trimmed string (stop at '\0'), while computing a stable 32-bit signature.
                        // Signature: FNV-1a over 16-bit code units.
                        uint h = 2166136261u;
                        var sb = new StringBuilder(n);

                        for (int i = 0; i < n; i++)
                        {
                            char c = ca[i];
                            if (c == '\0')
                                break;

                            // sanitize: keep printable ASCII, replace others with '.'
                            char s = (c >= 0x20 && c <= 0x7E) ? c : '.';
                            sb.Append(s);

                            ushort cu = c;
                            h ^= cu;
                            h *= 16777619u;
                        }

                        codeStr = sb.ToString();
                        sig = h;
                        return true;
                    }

                    // Sometimes these show up as arrays with a different generic arg (sbyte/byte) depending on bindings.
                    // In that case we can still compute a signature, but string decoding is unknown.
                    if (raw is Il2CppStructArray<byte> ba)
                    {
                        int n = Math.Min(8, ba.Length);
                        if (n <= 0) return false;

                        uint h = 2166136261u;
                        var sb = new StringBuilder(n);
                        for (int i = 0; i < n; i++)
                        {
                            byte b = ba[i];
                            if (b == 0)
                                break;

                            char s = (b >= 0x20 && b <= 0x7E) ? (char)b : '.';
                            sb.Append(s);

                            h ^= b;
                            h *= 16777619u;
                        }

                        codeStr = sb.ToString();
                        sig = h;
                        return true;
                    }
                }
                catch
                {
                    // ignore
                }

                return false;
            }

private static void TryDumpDds3GbwkUnitSnapshot(TextWriter w, int maxUnits, int maxStock)
            {
                try
                {
                    if (!TryGetDds3GlobalWorkObject(out object? dds3GbwkObj) || dds3GbwkObj == null)
                    {
                        w.WriteLine("(unavailable)");
                        return;
                    }

                    int stockCnt = TryReadInt32(dds3GbwkObj, "stockcnt", -1);
                    int maxStockCap = TryReadInt32(dds3GbwkObj, "maxstock", -1);

                    w.WriteLine($"DDS3_GBWK.type={dds3GbwkObj.GetType().FullName}  stockcnt={stockCnt}  maxstock={maxStockCap}");

                    // --- unitwork ---
                    if (TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) &&
                        unitworkArr != null &&
                        TryGetLengthOrCount(unitworkArr, out int uwLen, out string uwKind))
                    {
                        int showUnits = Math.Min(Math.Max(maxUnits, 0), uwLen);

                        w.WriteLine($"unitwork: kind={uwKind}  len={uwLen}  showing={showUnits}");

                        for (int i = 0; i < showUnits; i++)
                        {
                            object? unitObj = null;
                            try { unitObj = TryGetIndexValue(unitworkArr, i); } catch { unitObj = null; }

                            if (unitObj == null)
                            {
                                w.WriteLine($"  [{i}] (null)");
                                continue;
                            }

                            int id = TryReadInt32(unitObj, "id", -1);
                            int lvl = TryReadInt32(unitObj, "level", -1);
                            int hp = TryReadInt32(unitObj, "hp", -1);
                            int maxhp = TryReadInt32(unitObj, "maxhp", -1);
                            int mp = TryReadInt32(unitObj, "mp", -1);
                            int maxmp = TryReadInt32(unitObj, "maxmp", -1);
                            int uniqueId = TryReadInt32(unitObj, "uniqueid", -1);
                            int nameCode = TryReadInt32(unitObj, "namecode", -1);

                            string nameTag = "";
                            if (id >= 0 && TryGetDevilNameBestEffort(id, out string nm))
                                nameTag = nm;

                            if (!string.IsNullOrEmpty(nameTag))
                                w.WriteLine($"  [{i}] id={id} lvl={lvl} hp={hp}/{maxhp} mp={mp}/{maxmp} uniqueid={uniqueId} namecode={nameCode}  nameTag=\"{Safe(nameTag)}\"");
                            else
                                w.WriteLine($"  [{i}] id={id} lvl={lvl} hp={hp}/{maxhp} mp={mp}/{maxmp} uniqueid={uniqueId} namecode={nameCode}");
                        }
                    }
                    else
                    {
                        w.WriteLine("unitwork: (unavailable)");
                    }

                    // --- stocklist ---
                    int stockListShow = Math.Min(Math.Max(maxStock, 0), Math.Max(stockCnt, 0));
                    if (stockListShow <= 0)
                        stockListShow = Math.Min(Math.Max(maxStock, 0), 24);

                    if (stockListShow > 0)
                    {
                        w.WriteLine($"stocklist: stockcnt={stockCnt}  showing={stockListShow}  (indices -> unitworkIndex -> id/name)");

                        for (int i = 0; i < stockListShow; i++)
                        {
                            if (!TryGetIntArrayAt(dds3GbwkObj, "stocklist", i, out int uwIdx))
                            {
                                w.WriteLine($"  [{i}] (unavailable)");
                                continue;
                            }

                            string extra = "";
                            if (TryResolveUnitFromCandidateIndex(dds3GbwkObj, uwIdx, out UnitResolveInfo info))
                            {
                                if (!string.IsNullOrEmpty(info.NameTag))
                                    extra = $"  -> unitwork[{info.UnitworkIndex}] id={info.UnitId} lvl={info.Level} hp={info.HP}/{info.MaxHP} mp={info.MP}/{info.MaxMP} nameTag=\"{Safe(info.NameTag)}\"";
                                else
                                    extra = $"  -> unitwork[{info.UnitworkIndex}] id={info.UnitId} lvl={info.Level} hp={info.HP}/{info.MaxHP} mp={info.MP}/{info.MaxMP}";
                            }

                            w.WriteLine($"  [{i}] {uwIdx}{extra}");
                        }
                    }
                    else
                    {
                        w.WriteLine("stocklist: (unavailable)");
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}