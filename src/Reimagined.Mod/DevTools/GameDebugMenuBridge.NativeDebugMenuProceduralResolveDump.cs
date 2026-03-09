#nullable enable
using System;
using System.IO;
using System.Reflection;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // 
            // Best-effort resolver for the built-in (native) debug menu's "procedural" pages (ITEM/SKILL),
            // where the cursor list is not backed by a visible cmpDbgList_s[] array.
           
 
            private static void DumpNativeDebugMenuProceduralResolution(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- Procedural List Resolution (best-effort) ---");

                // Mode hint used by the native debug menu.
                // Observed in your dumps:
                //  - gSelSkill=2 while browsing ITEM list
                //  - gSelSkill=3 while browsing SKILL menus
                int gSelSkill = TryReadStaticInt(cmpTest, "gSelSkill", fallback: -1);
                int gCursorSection = TryReadStaticInt(cmpTest, "gCursorSection", fallback: -1);
                int gActive1 = TryReadStaticInt(cmpTest, "gActive1", fallback: -1);
                int gActive2 = TryReadStaticInt(cmpTest, "gActive2", fallback: -1);

                w.WriteLine($"gSelSkill={gSelSkill}  gCursorSection={gCursorSection}  gActive1={gActive1}  gActive2={gActive2}");
                w.WriteLine("note=gSelSkill is an internal phase/selector used by the native debug menu; it is NOT a reliable ITEM/SKILL indicator on every build/page.");

                int skillIdProp = TryReadStaticInt(cmpTest, "Skill_id", fallback: -1);
                if (skillIdProp >= 0)
                {
                    // In SKILL pages, cmpTest.Skill_id is the single highest-signal anchor, but it is reused across phases:
                    //  - during demon selection, it often behaves like a devil id
                    //  - during skill selection, it behaves like a skill id
                    if (TryGetSkillNameBestEffort(skillIdProp, 0, out var skillNameProp))
                        w.WriteLine($"Skill_id={skillIdProp}  skill=\"{ShowBlankName(skillNameProp)}\"{(string.IsNullOrEmpty(skillNameProp) ? "  note=blank name" : "")}");
                    else if (TryGetDevilNameBestEffort(skillIdProp, out var devilNameProp))
                        w.WriteLine($"Skill_id={skillIdProp}  devil=\"{ShowBlankName(devilNameProp)}\"{(string.IsNullOrEmpty(devilNameProp) ? "  note=blank name" : "")}  note=not a skill id on this page/phase");
                    else
                        w.WriteLine($"Skill_id={skillIdProp}  (unresolved as skill or devil)");
                }
                else
                {
                    w.WriteLine($"Skill_id={skillIdProp}");
                }

                w.WriteLine();

                if (!TryReadStaticMember(cmpTest, "gCursorInfo", out var cursorArr) || cursorArr == null)
                {
                    w.WriteLine("cursorInfo=missing (cmpTest.gCursorInfo is null)");
                    return;
                }

                //  capture a compact snapshot up-front, so dumps are greppable.
                if (TryCaptureNativeDebugMenuSelectionSnapshot(cmpTest, out var snap))
                {
                    WriteNativeDebugMenuSelectionSnapshot(w, snap);
                    w.WriteLine();
                }

                int cursorLen = GetIl2CppRefArrayLength(cursorArr);
                if (cursorLen <= 0)
                {
                    w.WriteLine($"cursorInfo.len={cursorLen} (unexpected)");
                    return;
                }

                int activeIdx = (gCursorSection >= 0 && gCursorSection < cursorLen) ? gCursorSection : -1;

                // Heuristic fallback: pick the first cursor section that reports a non-zero ListNums.
                if (activeIdx < 0)
                {
                    for (int i = 0; i < cursorLen; i++)
                    {
                        var ci = GetIl2CppRefArrayItem(cursorArr, i);
                        if (ci == null) continue;

                        var cp = GetProp(ci, "CursorPos");
                        int listNums = GetIntProp(cp, "ListNums", 0);
                        if (listNums > 0)
                        {
                            activeIdx = i;
                            break;
                        }
                    }
                }

                if (activeIdx < 0)
                {
                    w.WriteLine("cursorInfo.active=unknown (no section reported ListNums>0)");
                    return;
                }

                var cursorInfo = GetIl2CppRefArrayItem(cursorArr, activeIdx);
                if (cursorInfo == null)
                {
                    w.WriteLine($"cursorInfo.activeIdx={activeIdx} (null item)");
                    return;
                }

                var cursorPos = GetProp(cursorInfo, "CursorPos");
                int activeListNums = GetIntProp(cursorPos, "ListNums", 0);
                int activeShiftMax = GetIntProp(cursorPos, "ShiftMax", 0);
                int activeIndex = GetIntProp(cursorPos, "Index", 0);
                int activeShift = GetIntProp(cursorPos, "Shift", 0);
                int activeSel = activeIndex + activeShift;

                w.WriteLine($"cursorInfo.activeIdx={activeIdx}");
                w.WriteLine($"cursorPos.listNums={activeListNums}  shiftMax={activeShiftMax}  index={activeIndex}  shift={activeShift}  sel={activeSel}");
                w.WriteLine();

                // Infer which root menu we are under (ITEM/SKILL/etc) using cursor[0] + cmpDbgRootList.
                // This is more reliable than gSelSkill in some builds (your Pass83 run had gSelSkill=0 even inside ITEM list).
                string? rootWord = TryInferRootMenuWord(cmpTest, cursorArr);
                if (!string.IsNullOrEmpty(rootWord))
                    w.WriteLine($"rootMenu.word=\"{rootWord}\"  (inferred from cursor[0] + cmpDbgRootList)");
                else
                    w.WriteLine("rootMenu.word=(unknown)");

                // Decide which mapping to print.
                // Priority order:
                //   1) root menu word (ITEM/SKILL)
                //   2) cursor.listNums matches datItemName/datSkillName length
                //   3) gSelSkill hint (2/3)
                //   4) otherwise print both
                var mode = InferMode(rootWord, activeListNums, gSelSkill, out string reason);

                w.WriteLine($"inferredMode={mode}  reason={reason}");
                w.WriteLine();

                // If this is not a "large, paged" list, we still print mapping attempts, but mark it as low-confidence.
                bool looksPaged = (activeListNums >= 32) && (activeShiftMax > 0) && (activeShiftMax < activeListNums);
                if (!looksPaged)
                {
                    w.WriteLine("note=cursor does not look like a paged large list; mapping attempts below may be irrelevant (submenu/category list).");
                    w.WriteLine();
                }

                
                // -------------------------------------------------------------
                //  SKILL "select a demon" page is a small procedural list.
                //
                bool didSkillDemonMap = false;
                if (ShouldDumpSkillDemonSelection(rootWord, activeListNums, activeShiftMax))
                {
                    didSkillDemonMap = true;
                    DumpSkillDemonSelectionMapping(cmpTest, w, activeIdx, activeListNums, activeSel, gActive1, gActive2);
                    w.WriteLine();
                }

                if (mode == ProcMode.Item)
                    DumpItemNameMapping(activeListNums, activeSel, w);
                else if (mode == ProcMode.Skill)
                {
                    // In the SKILL list phase (gActive1=1 gActive2=1), the real highlight is cmpTest.Skill_id.
                    // Cursor sel heuristics are misleading there, so we suppress the candidate triplet to reduce noise.
                    if (gActive1 == 1 && gActive2 == 1)
                    {
                        w.WriteLine("--- SKILL mapping (datSkillName) ---");
                        w.WriteLine("note=skipped cursor-based candidates because this dump is already in the active skill list phase (gActive1=1 gActive2=1).");
                        w.WriteLine("hint=use Skill_id above (and the snapshot header) as the authoritative highlighted skill id.");
                    }
                    else if (didSkillDemonMap)
                    {
                        w.WriteLine("--- SKILL mapping (datSkillName) ---");
                        w.WriteLine("note=skipped cursor-based candidates because the active cursor appears to be the demon list (resolved above).");
                    }
                    else
                    {
                        DumpSkillNameMapping(activeListNums, activeSel, w);
                    }
                }
                else
                {
                    w.WriteLine("note=mode unknown; printing BOTH item and skill mapping candidates for quick eyeballing.");
                    w.WriteLine();
                    DumpItemNameMapping(activeListNums, activeSel, w);
                    w.WriteLine();
                    DumpSkillNameMapping(activeListNums, activeSel, w);
                }

                if (mode == ProcMode.Skill)
                {
                    // In several builds/pages, cmpTest.Skill_id tracks the *actual* highlighted/selected thing on SKILL pages
                    // even when cursor.sel is just a small list index (category list, demon list, etc).
                    if (skillIdProp >= 0)
                    {
                        w.WriteLine();

                        if (TryGetSkillNameBestEffort(skillIdProp, 0, out var skillNameProp2))
                        {
                            var _dispSkillName = string.IsNullOrEmpty(skillNameProp2) ? "<blank>" : skillNameProp2;
                            w.WriteLine($"anchor=cmpTest.Skill_id={skillIdProp}  skill=\"{_dispSkillName}\"");
                            if (!looksPaged && skillIdProp != activeSel)
                                w.WriteLine("note=cursor.sel is a small-list index on this page; Skill_id is often the real skill id.");
                        }
                        else if (TryGetDevilNameBestEffort(skillIdProp, out var devilNameProp2))
                        {
                            var _dispDevilName = string.IsNullOrEmpty(devilNameProp2) ? "<blank>" : devilNameProp2;
                            w.WriteLine($"anchor=cmpTest.Skill_id={skillIdProp}  devil=\"{_dispDevilName}\"");
                            if (!looksPaged)
                                w.WriteLine("note=cursor.sel is likely the demon-list row; Skill_id behaves like a devil id in this phase.");
                        }
                        else
                        {
                            w.WriteLine($"anchor=cmpTest.Skill_id={skillIdProp}  (unresolved as skill or devil)");
                        }
                    }
                }
            }

            
            private static bool ShouldDumpSkillDemonSelection(string? rootWord, int listNums, int shiftMax)
            {
                if (listNums != 9)
                    return false;

                if (shiftMax != 9 && shiftMax != 0)
                    return false;

                // If we can infer the root word, require it to look like the SKILL branch.
                if (!string.IsNullOrEmpty(rootWord))
                {
                    if (rootWord.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                   
                }

                return true;
            }

            private static void DumpSkillDemonSelectionMapping(Type cmpTest, TextWriter w, int activeCursorIdx, int listNums, int sel, int gActive1, int gActive2)
            {
                w.WriteLine("--- Demon Selection Resolution (SKILL, best-effort) ---");
                w.WriteLine($"cursor.activeIdx={activeCursorIdx}  listNums={listNums}  sel={sel}");
                w.WriteLine($"phaseHint=gActive1:{gActive1} gActive2:{gActive2}  (expected demon-select signature: gActive1=1 gActive2=0)");

                if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                {
                    w.WriteLine("DDS3_GBWK: (unavailable)");
                    w.WriteLine("note=If this is unavailable in camp, try dumping during a stable menu state (command menu open) or after re-opening the debug menu.");
                    return;
                }

                int stockCnt = TryReadInt32(gbwkObj, "stockcnt", -1);
                int maxStock = TryReadInt32(gbwkObj, "maxstock", -1);

                var mappingKind = DetectSkillDemonSlotMappingKind(gbwkObj, w);

                w.WriteLine($"DDS3_GBWK.stockcnt={stockCnt}  maxstock={maxStock}  mapping={mappingKind}");

                if (mappingKind == SkillDemonSlotMappingKind.Stocklist0IsProtag)
                {
                    w.WriteLine("slotMapping: slot0..8=stocklist[slot] -> unitwork  (stocklist includes protagonist at [0] on this build; native debug menu often shows their name as \"------\")");
                }
                else
                {
                    w.WriteLine("slotMapping: slot0=unitwork[0] (protagonist; native debug menu often shows \"------\")  slot1..8=stocklist[slot-1] -> unitwork");
                }

                w.WriteLine("slots.cols=mark slot stockIdxUsed unitworkIdx id lvl hp/mp nameTag namecode fullname");

                for (int slot = 0; slot < 9; slot++)
                {
                    bool isSel = (slot == sel);
                    string mark = isSel ? ">>" : "  ";

                    if (TryResolveSkillDemonSlot(gbwkObj, slot, mappingKind, out int stockIdxUsed, out UnitResolveInfo info))
                    {
                        string nm = !string.IsNullOrEmpty(info.NameTag) ? info.NameTag : "(no devil name)";
                        string stockStr = stockIdxUsed >= 0 ? stockIdxUsed.ToString() : "-";
                        w.WriteLine($"{mark} slot={slot} stockIdxUsed={stockStr}  unitworkIdx={info.UnitworkIndex} id={info.UnitId} lvl={info.Level} hp={info.HP}/{info.MaxHP} mp={info.MP}/{info.MaxMP} nameTag=\"{Safe(nm)}\" namecode=\"{Safe(info.NameCodeStr)}\" fullname=\"{Safe(info.FullNameCodeStr)}\"");
                    }
                    else
                    {
                        w.WriteLine($"{mark} slot={slot} (unresolved)");
                    }
                }

                // Emit a compact highlight line for quick grepping.
                if (sel >= 0 && sel < 9)
                {
                    if (TryResolveSkillDemonSlot(gbwkObj, sel, mappingKind, out int hiStockIdx, out UnitResolveInfo hi))
                    {
                        string nm = !string.IsNullOrEmpty(hi.NameTag) ? hi.NameTag : "(no devil name)";
                        if (hiStockIdx >= 0)
                            w.WriteLine($"highlight.slot={sel} -> stocklist[{hiStockIdx}] -> unitwork[{hi.UnitworkIndex}] id={hi.UnitId} name=\"{Safe(nm)}\"");
                        else
                            w.WriteLine($"highlight.slot={sel} -> unitwork[{hi.UnitworkIndex}] id={hi.UnitId} name=\"{Safe(nm)}\"");
                    }
                    else
                    {
                        w.WriteLine($"highlight.slot={sel} -> (unresolved)");
                    }
                }
            }

private enum ProcMode
            {
                Unknown = 0,
                Item = 1,
                Skill = 2,
            }

            private static ProcMode InferMode(string? rootWord, int listNums, int gSelSkill, out string reason)
            {
                reason = "";

                if (!string.IsNullOrEmpty(rootWord))
                {
                    if (string.Equals(rootWord, "ITEM", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "root menu highlights ITEM";
                        return ProcMode.Item;
                    }
                    if (string.Equals(rootWord, "SKILL", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "root menu highlights SKILL";
                        return ProcMode.Skill;
                    }
                }

                // Length-based fallback: if cursor.listNums matches the table length exactly, it's almost certainly that page.
                // Note: we only use this as a hint; if txt.len is unavailable (-1), we skip it.
                int itemLen = TryGetIl2CppTxtLength("Il2Cpp.datItemName", "datItemName");
                if (itemLen > 0 && listNums == itemLen)
                {
                    reason = $"cursor.listNums ({listNums}) matches datItemName.txt.len ({itemLen})";
                    return ProcMode.Item;
                }

                int skillLen = TryGetIl2CppTxtLength("Il2Cpp.datSkillName", "datSkillName");
                if (skillLen > 0 && listNums == skillLen)
                {
                    reason = $"cursor.listNums ({listNums}) matches datSkillName.txt.len ({skillLen})";
                    return ProcMode.Skill;
                }
                reason = $"no reliable mode signal (rootWord != ITEM/SKILL; listNums != known table lens; gSelSkill={gSelSkill})";
                return ProcMode.Unknown;
            }

            private static string? TryInferRootMenuWord(Type cmpTest, object cursorArr)
            {
                try
                {
                    if (!TryReadStaticMember(cmpTest, "cmpDbgRootList", out var rootListObj) || rootListObj == null)
                        return null;

                    // Root cursor is always section 0.
                    var rootCursorInfo = GetIl2CppRefArrayItem(cursorArr, 0);
                    if (rootCursorInfo == null)
                        return null;

                    var rootCursorPos = GetProp(rootCursorInfo, "CursorPos");
                    int rootIndex = GetIntProp(rootCursorPos, "Index", 0);
                    int rootShift = GetIntProp(rootCursorPos, "Shift", 0);
                    int rootSel = rootIndex + rootShift;

                    int rootLen = GetIl2CppRefArrayLength(rootListObj);
                    if (rootSel < 0 || rootSel >= rootLen)
                        return null;

                    var entry = GetIl2CppRefArrayItem(rootListObj, rootSel);
                    if (entry == null)
                        return null;

                    return GetStringProp(entry, "word");
                }
                catch
                {
                    return null;
                }
            }

            private static void DumpItemNameMapping(int listNums, int sel, TextWriter w)
            {
                int txtLen = TryGetIl2CppTxtLength("Il2Cpp.datItemName", "datItemName");

                w.WriteLine("--- ITEM mapping (datItemName) ---");
                w.WriteLine($"datItemName.txt.len={txtLen}  cursor.listNums={listNums}  sel={sel}");
                w.WriteLine("hypothesis=the highlighted itemId is either (sel-1), (sel), or (sel+1) depending on 0/1-based indexing.");
                w.WriteLine("candidates (sel-1 / sel / sel+1):");

                DumpIdNameTriplet(
                    idA: sel - 1,
                    idB: sel,
                    idC: sel + 1,
                    nameGetter: static (int id) =>
                    {
                        if (TryGetItemName(id, out var name))
                            return name;
                        return null;
                    },
                    w: w
                );
            }

            private static void DumpSkillNameMapping(int listNums, int sel, TextWriter w)
            {
                int txtLen = TryGetIl2CppTxtLength("Il2Cpp.datSkillName", "datSkillName");

                w.WriteLine("--- SKILL mapping (datSkillName) ---");
                w.WriteLine($"datSkillName.txt.len={txtLen}  cursor.listNums={listNums}  sel={sel}");
                w.WriteLine("hypothesis=the highlighted skillId is either (sel-1), (sel), or (sel+1) depending on 0/1-based indexing.");
                w.WriteLine("candidates (sel-1 / sel / sel+1):");

                DumpIdNameTriplet(
                    idA: sel - 1,
                    idB: sel,
                    idC: sel + 1,
                    nameGetter: static (int id) =>
                    {
                        if (TryGetSkillNameBestEffort(id, 0, out var name))
                            return name;
                        return null;
                    },
                    w: w
                );
            }

            private static void DumpIdNameTriplet(int idA, int idB, int idC, Func<int, string?> nameGetter, TextWriter w)
            {
                DumpSingleIdName(idA, nameGetter, w);
                DumpSingleIdName(idB, nameGetter, w);
                DumpSingleIdName(idC, nameGetter, w);
            }

            private static void DumpSingleIdName(int id, Func<int, string?> nameGetter, TextWriter w)
            {
                if (id < 0)
                {
                    w.WriteLine($"  id={id,-6}  name=<skip negative>");
                    return;
                }

                string? name = null;
                string? err = null;

                try
                {
                    name = nameGetter(id);
                }
                catch (Exception ex)
                {
                    err = ex.GetType().Name;
                }

                if (!string.IsNullOrEmpty(err))
                {
                    w.WriteLine($"  id={id,-6}  name=<ERR:{err}>");
                    return;
                }

                if (string.IsNullOrEmpty(name))
                {
                    w.WriteLine($"  id={id,-6}  name=<none>");
                    return;
                }

                // Normalize for logs.
                name = name.Replace("\r", "").Replace("\n", "\\n");
                if (name.Length > 160)
                    name = name.Substring(0, 160) + "...";

                w.WriteLine($"  id={id,-6}  name=\"{name}\"");
            }

            // 
            // datItemName/datSkillName keep a static 'txt' that behaves like an array/list.
            // We read it via reflection purely for a sanity-print (len), never as a hard requirement.
            // 
            private static int TryGetIl2CppTxtLength(string fullNameA, string fullNameB)
            {
                try
                {
                    var t = FindTypeInLoadedAssemblies(fullNameA) ?? FindTypeInLoadedAssemblies(fullNameB);
                    if (t == null)
                        return -1;

                    object? txt = null;

                    var f = t.GetField("txt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (f != null)
                    {
                        try { txt = f.GetValue(null); } catch { txt = null; }
                    }

                    if (txt == null)
                    {
                        var p = t.GetProperty("txt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (p != null)
                        {
                            try { txt = p.GetValue(null); } catch { txt = null; }
                        }
                    }

                    if (txt == null)
                        return -1;

                    return TryGetLengthLike(txt);
                }
                catch
                {
                    return -1;
                }
            }

            private static int TryGetLengthLike(object obj)
            {
                var t = obj.GetType();

                // Common patterns across Il2CppInterop arrays/lists.
                var p = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)
                     ?? t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);

                if (p != null)
                {
                    object? v = null;
                    try { v = p.GetValue(obj); } catch { v = null; }
                    if (v != null)
                    {
                        try { return Convert.ToInt32(v); } catch { /* ignore */ }
                    }
                }

                return -1;
            }

            // NOTE: TryReadStaticInt(Type,string,int) is defined elsewhere in GameDebugMenuBridge
            // (shared by multiple partial files). Keep only one definition to avoid CS0111.

            private static string ShowBlankName(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "<blank>";
                return s!;
            }

        }
    }
}