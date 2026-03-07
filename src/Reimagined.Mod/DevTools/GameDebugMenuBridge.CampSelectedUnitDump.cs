#nullable enable
using System;
using System.IO;
using System.Text;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass 74: Selected UnitWork surface dump (selection-driven)
            // =========================================================
            // Motivation:
            // - We can now reliably resolve the highlighted demon to a UnitWork index.
            // - The next unlock is mapping which UnitWork fields are truly stable/meaningful
            //   (unique ids, roster indices, flags, skill arrays, etc.).
            // - Dumping the entire camp reflection surface is too heavy for fast iteration.
            // - This hotkey targets ONLY the resolved highlighted unit.

            internal static void HotkeyDumpCampSelectedUnitSurface()
            {
                string path = MakeDumpPath("camp_selected_unit_surface");
                MelonLogger.Msg($"[Reimagined] Camp selected-unit surface dump -> {path}");

                try
                {
                    using var w = new StreamWriter(path, append: false, Encoding.UTF8);
                    w.WriteLine("=== Camp Selected Unit Surface Dump ===");
                    w.WriteLine("dumpSchema=CampSelectedUnitSurface v1");
                    w.WriteLine($"timeLocal={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    w.WriteLine($"timeUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}  ticksUtc={DateTime.UtcNow.Ticks}");
                    w.WriteLine();

                    if (!TryGetCampHighlightedSelection(out CampHighlightedSelection sel, out string? selErr))
                    {
                        w.WriteLine("ERROR: Failed to resolve highlighted selection.");
                        w.WriteLine($"error={Safe(selErr)}");
                        return;
                    }

                    // Always print the stable key + reason so this dump can be correlated against selection summaries.
                    w.WriteLine("-- selection --");
                    w.WriteLine($"kind={sel.Kind}  confidence={sel.Confidence}");
                    w.WriteLine($"cursor={sel.Cursor}  idx={sel.Index} shift={sel.Shift} overall={sel.Overall}");
                    w.WriteLine($"listIdx={sel.ListIdxVal}  stockIdx={sel.StockIdxVal}  unitworkIndex={sel.Unit.UnitworkIndex}");
                    w.WriteLine($"listNums={sel.ListNums}  partyCnt={sel.PartyCnt}  stockCnt={sel.StockCnt}  rowKind={sel.RowKind}");
                    w.WriteLine($"key={BuildStableHighlightKey(sel)}");
                    w.WriteLine($"reason=\"{Safe(sel.Reason)}\"");
                    w.WriteLine();

                    // Resolve the underlying UnitWork object.
                    if (!TryGetDds3GlobalWorkObject(out object? dds3GbwkObj) || dds3GbwkObj == null)
                    {
                        w.WriteLine("ERROR: dds3GlobalWork.DDS3_GBWK unavailable.");
                        return;
                    }

                    int uwIdx = sel.Unit.UnitworkIndex;
                    if (uwIdx < 0)
                    {
                        w.WriteLine("ERROR: selection produced an invalid UnitWork index.");
                        return;
                    }

                    if (!TryGetInstanceMemberValue(dds3GbwkObj, "unitwork", out object? unitworkArr) || unitworkArr == null)
                    {
                        w.WriteLine("ERROR: dds3GBWK.unitwork unavailable.");
                        return;
                    }

                    if (!TryGetLengthOrCount(unitworkArr, out int uwLen, out _))
                    {
                        w.WriteLine("ERROR: could not determine unitwork length.");
                        return;
                    }

                    if (uwIdx >= uwLen)
                    {
                        w.WriteLine($"ERROR: unitworkIndex out of range: {uwIdx} >= {uwLen}");
                        return;
                    }

                    object? unitObj = null;
                    try { unitObj = TryGetIndexValue(unitworkArr, uwIdx); } catch { unitObj = null; }

                    if (unitObj == null)
                    {
                        w.WriteLine("ERROR: unitwork[unitworkIndex] was null/unavailable.");
                        return;
                    }

                    // Quick headline derived from our existing resolver (helps sanity-check).
                    w.WriteLine("-- resolved headline (from UnitResolveInfo) --");
                    if (TryGetDds3GlobalWorkObject(out object? gbwkObj) && gbwkObj != null)
                    {
                        int sc = TryReadInt32(gbwkObj, "stockcnt", -1);
                        int ms = TryReadInt32(gbwkObj, "maxstock", -1);
                        if (sc >= 0 || ms >= 0)
                            w.WriteLine($"gbwk.stockcnt={sc} (stocklist count = party+stock)  gbwk.maxstock={ms} (stock capacity)");
                    }
                    string hiName = sel.Unit.NameTag ?? string.Empty;

                    string ncHex = "";
                    string fncHex = "";
                    TryReadCharCode8Hex(unitObj, "namecode", out ncHex);
                    TryReadCharCode8Hex(unitObj, "fullnamecode", out fncHex);

                    w.WriteLine($"ptr=0x{sel.Unit.Ptr:X}  id={sel.Unit.UnitId}  uid={sel.Unit.UniqueId}  ncSig=0x{sel.Unit.NameCodeSig:X8}  ncHex={ncHex}  nc=\"{Safe(sel.Unit.NameCodeStr ?? string.Empty)}\"  fncSig=0x{sel.Unit.FullNameCodeSig:X8}  fncHex={fncHex}  fnc=\"{Safe(sel.Unit.FullNameCodeStr ?? string.Empty)}\"  lvl={sel.Unit.Level}  hp={sel.Unit.HP}/{sel.Unit.MaxHP}  mp={sel.Unit.MP}/{sel.Unit.MaxMP}  name=\"{Safe(hiName)}\"");
                    w.WriteLine();

                    // Skill list (read-only, diff-friendly): skill ids + localized labels.
                    TryWriteUnitSkillSummary(w, unitObj, sel.Unit.UnitId);
                    w.WriteLine();

                    // Filtered dump first (most useful, fewer lines, better diffs).
                    w.WriteLine("-- UnitWork (filtered) --");
                    string[] tokens = new[]
                    {
                        "id", "unique", "uid", "index", "party", "stock",
                        "level", "hp", "mp", "exp", "cost", "flag", "state", "status",
                        "name", "race", "devil", "skill", "magic", "param", "attr", "resist",
                        "ai", "event", "item", "equip", "demon", "unit"
                    };
                    DumpObjectMembersFiltered(w, "UnitWork(filtered)", unitObj, tokens, maxLines: 360);
                    w.WriteLine();

                    // All-members dump as a follow-up (still capped).
                    w.WriteLine("-- UnitWork (all members; capped) --");
                    DumpObjectMembersAll(w, "UnitWork(all)", unitObj, maxLines: 420);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Reimagined] Camp selected-unit surface dump failed: {ex.GetType().Name}: {ex.Message}");
                }
            }


            private static bool TryReadCharCode8Hex(object owner, string memberName, out string hex)
            {
                hex = "";
                try
                {
                    if (!TryGetInstanceMemberValue(owner, memberName, out object? raw) || raw == null)
                        return false;

                    if (!TryGetLengthOrCount(raw, out int n, out string _kind) || n <= 0)
                        return false;

                    int take = Math.Min(8, n);
                    var sb = new StringBuilder();
                    for (int i = 0; i < take; i++)
                    {
                        if (i != 0) sb.Append(' ');
                        object? v = TryGetIndexValue(raw, i);
                        int cu = TryCoerceInt32(v, 0);
                        sb.Append(((ushort)cu).ToString("X4"));
                    }

                    hex = sb.ToString();
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static void TryWriteUnitSkillSummary(StreamWriter w, object unitObj, int devilId)
            {
                try
                {
                    int skillCnt = TryReadInt32(unitObj, "skillcnt", -1);
                    if (skillCnt < 0)
                    {
                        w.WriteLine("skills: (skillcnt unavailable)");
                        return;
                    }

                    if (!TryGetInstanceMemberValue(unitObj, "skill", out object? rawSkillArr) || rawSkillArr == null)
                    {
                        w.WriteLine($"skills: skillcnt={skillCnt}  (skill array unavailable)");
                        return;
                    }

                    int arrLen = -1;
                    if (TryGetLengthOrCount(rawSkillArr, out int n, out string _kind2))
                        arrLen = n;

                    int take = skillCnt;
                    if (arrLen >= 0) take = Math.Min(take, arrLen);
                    take = Math.Min(take, 24);

                    // FNV-1a over the declared skills (best-effort, for diffing).
                    uint sig = 2166136261u;
                    var sb = new StringBuilder();

                    w.WriteLine($"skills: skillcnt={skillCnt}  arrLen={arrLen}  take={take}");

                    for (int i = 0; i < take; i++)
                    {
                        if (!TryGetIntArrayAt(unitObj, "skill", i, out int sid))
                            continue;

                        sig ^= unchecked((uint)sid);
                        sig *= 16777619u;

                        string nm = "";
                        if (TryGetSkillNameBestEffort(sid, devilId, out string sname))
                            nm = sname;

                        if (sb.Length == 0) sb.Append("skillsList=");
                        else sb.Append(", ");
                        if (!string.IsNullOrEmpty(nm))
                            sb.Append(sid).Append(':').Append('"').Append(Safe(nm)).Append('"');
                        else
                            sb.Append(sid);

                        w.WriteLine($"  [{i,2}] {sid,5}  {(string.IsNullOrEmpty(nm) ? "" : Safe(nm))}");
                    }

                    w.WriteLine($"skills.sig=0x{sig:X8}");
                    if (sb.Length > 0)
                        w.WriteLine(sb.ToString());
                }
                catch (Exception ex)
                {
                    w.WriteLine($"skills: (failed) {ex.GetType().Name}: {Safe(ex.Message)}");
                }
            }

        }
    }
}
