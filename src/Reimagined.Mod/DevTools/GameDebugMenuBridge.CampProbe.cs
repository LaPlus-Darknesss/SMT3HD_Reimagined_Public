#nullable enable
using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        // Split-out helper code for probing vanilla Command Menu (Camp Menu) state.
        // This is intentionally observation-only: it should never change game state.
        private static partial class GameDebugMenuBridge
        {

            internal enum CampSubCursorKind
            {
                                Unknown = -1,
None = 0,
                Item = 1,
                Party = 2,
                Hearts = 3,
                System = 4,
            }

private struct CampProbeState
            {
                public bool HaveCmpInit;

                public bool HaveProcStat;
                public sbyte ProcStat;

                public bool HaveExitState;
                public sbyte ExitState;

                public bool HaveSeqGate;
                public int GateCamp;

                public bool HaveGbFlag;
                public sbyte GbFlag;

                public bool HaveCommandFlag;
                public int CommandFlagLen;
                public string CommandFlagHex;
                public int CommandFlagHash;

                public bool HaveMenuIndex;
                public int MenuIndex;
                public bool HaveItemIndex;
                public int ItemIndex;
                public bool HaveArrowIndex;
                public int ArrowIndex;
                public bool HavePartyIndex;
                public int PartyIndex;
                public bool HaveStockIndex;
                public int StockIndex;
                public bool HaveDrawIndex;
                public int DrawIndex;

                // Derived cursor selections from cmpCursorInfo_t arrays (RootCI/WorkCI).
                public bool HaveRootCursor;
                public int RootIndex;
                public int RootShift;
                public int RootListNums;
                public int RootSel;

                public bool HaveWorkCursor;
                public int WorkIndex;
                public int WorkShift;
                public int WorkListNums;
                public int WorkSel;

                public bool HaveItemCnt;
                public int ItemCnt;

                public bool HaveItemSel;
                public int ItemSelId;
                public string ItemSelName;

                public bool HaveSelectionLocalized;
                public string SelectionLocalized;

                public bool HaveSeqInfo;
                public sbyte SeqCur;
                public sbyte SeqNext;
                public sbyte SeqLast;
                public sbyte SeqChange;
                public sbyte SeqFlag;
                public sbyte SeqMesFlag;
                public sbyte SeqTimer;

                public bool HaveCampUI;
                public bool CampUIActive;

                // campUI has a live GameObject reference (menuCur) which is useful for correlating state.

                public bool HaveMenuCurName;
                public string MenuCurName;

                public bool HaveMenuSlotLen;
                public int MenuSlotLen;

                public bool HaveRootMenuLen;
                public int RootMenuLen;

                public bool HaveRootSubLen;
                public int RootSubLen;

                public bool HaveMenuListActive;
                public bool MenuListActive;

                public bool HaveItemListActive;
                public bool ItemListActive;

                public bool HaveSelectionLabel;
                public string SelectionLabel;

                // Active "secondary" cursor (depends on which root entry is selected).
                // Observed mapping from CMP_GBWK.RootCI[] (NA/Steam):
                //   RootCI[1] = Item submenu (Use / Discard / Gems / Key Items)
                //   RootCI[2] = Party submenu (Summon / Return to stock / Part with)
                //   RootCI[3] = Magatama submenu (Equip / Unequip)
                //   RootCI[4] = System submenu (Config / Load / Suspend / Title Screen / End Game)
                public CampSubCursorKind SubKind;
                public int SubCiIndex;

                public bool HaveSubCursor;
                public int SubIndex;
                public int SubShift;
                public int SubListNums;
                public int SubSel;

                public bool HaveSubSelectionLabel;
                public string SubSelectionLabel;

                public bool HaveSubSelectionLocalized;
                public string SubSelectionLocalized;
            }
// Cached (seqTrace) to reduce log spam.
            private static int s_seqTraceLastCampGate = int.MinValue;
            private static int s_seqTraceLastCmpProc = int.MinValue;
            private static int s_seqTraceLastCmpExit = int.MinValue;
            private static int s_seqTraceLastCampUIActive = int.MinValue;
            private static int s_seqTraceLastMenuIndex = int.MinValue;
            private static int s_seqTraceLastSubIndex = int.MinValue;
            private static int s_seqTraceLastSubLabelHash = int.MinValue;
            private static int s_seqTraceLastSubKind = int.MinValue;
            private static int s_seqTraceLastPartyIndex = int.MinValue;
            private static int s_seqTraceLastStockIndex = int.MinValue;
            private static int s_seqTraceLastArrowIndex = int.MinValue;
            private static int s_seqTraceLastItemIndex = int.MinValue;
            private static int s_seqTraceLastItemSelId = int.MinValue;
            private static int s_seqTraceLastCmdHash = int.MinValue;
            private static int s_seqTraceLastCampMenuLen = int.MinValue;
            private static int s_seqTraceLastCampMenuSlots = int.MinValue;
            private static int s_seqTraceLastCampMenuScreen = int.MinValue;
            private static int s_seqTraceLastCampMenuLabelHash = int.MinValue;
            private static int s_seqTraceLastCampSeqPack = int.MinValue;

            private static void ResetCampTraceLatches()
            {
                s_seqTraceLastCampGate = int.MinValue;
                s_seqTraceLastCmpProc = int.MinValue;
                s_seqTraceLastCmpExit = int.MinValue;
                s_seqTraceLastCampUIActive = int.MinValue;
                s_seqTraceLastMenuIndex = int.MinValue;
                s_seqTraceLastSubIndex = int.MinValue;
                s_seqTraceLastSubLabelHash = int.MinValue;
                s_seqTraceLastSubKind = int.MinValue;
                s_seqTraceLastPartyIndex = int.MinValue;
                s_seqTraceLastStockIndex = int.MinValue;
                s_seqTraceLastArrowIndex = int.MinValue;
                s_seqTraceLastItemIndex = int.MinValue;
                s_seqTraceLastItemSelId = int.MinValue;
                s_seqTraceLastCmdHash = int.MinValue;
                s_seqTraceLastCampMenuLen = int.MinValue;
                s_seqTraceLastCampMenuSlots = int.MinValue;
                s_seqTraceLastCampMenuScreen = int.MinValue;
                s_seqTraceLastCampMenuLabelHash = int.MinValue;
                s_seqTraceLastCampSeqPack = int.MinValue;
            }

            private static int PackSeqInfo(sbyte cur, sbyte next, sbyte last, sbyte change, sbyte flag, sbyte mesFlag, sbyte timer)
            {
                // Pack into a stable int for quick change detection.
                // (sbyte -> byte cast) avoids sign-extension differences.
                unchecked
                {
                    int p = 0;
                    p |= ((byte)cur) << 0;
                    p |= ((byte)next) << 8;
                    p |= ((byte)last) << 16;
                    p |= ((byte)change) << 24;
                    // We also want flag/mesFlag/timer, but we're out of bits.
                    // Fold them in with a small hash.
                    p ^= (((byte)flag) * 131) ^ (((byte)mesFlag) * 17) ^ (((byte)timer) * 3);
                    return p;
                }
            }

            private static bool TryGetCampProbeState(out CampProbeState st)
            {
                st = default;
                try
                {
                    var cmpInit = FindTypeInLoadedAssemblies("Il2Cpp.cmpInit");
                    st.HaveCmpInit = (cmpInit != null);
                    if (cmpInit == null)
                        return false;

                    // Process state for the camp/menu system.
                    sbyte ps = 0;
                    if (TryReadSByteStatic(cmpInit, "gProcessStat", out ps))
                    {
                        st.HaveProcStat = true;
                        st.ProcStat = ps;
                    }

                    if (TryReadSByteStatic(cmpInit, "gExitState", out sbyte ex))
                    {
                        st.HaveExitState = true;
                        st.ExitState = ex;
                    }

                    // gCommandFlag (byte array): extremely useful for mapping how the command menu state machine works.
                    if (TryReadByteArrayHexStatic(cmpInit, "gCommandFlag", 16, out int cLen, out string cHex, out int cHash))
                    {
                        st.HaveCommandFlag = true;
                        st.CommandFlagLen = cLen;
                        st.CommandFlagHex = cHex;
                        st.CommandFlagHash = cHash;
                    }

                    // Index globals (move up/down / enter submenu should change one or more of these).
                    if (TryReadIntStatic(cmpInit, "MenuIndex", out int mi)) { st.HaveMenuIndex = true; st.MenuIndex = mi; }
                    if (TryReadIntStatic(cmpInit, "ItemIndex", out int ii)) { st.HaveItemIndex = true; st.ItemIndex = ii; }
                    if (TryReadIntStatic(cmpInit, "ArrowIndex", out int ai)) { st.HaveArrowIndex = true; st.ArrowIndex = ai; }
                    if (TryReadIntStatic(cmpInit, "PartyIndex", out int pi)) { st.HavePartyIndex = true; st.PartyIndex = pi; }
                    if (TryReadIntStatic(cmpInit, "StockIndex", out int si)) { st.HaveStockIndex = true; st.StockIndex = si; }
                    if (TryReadIntStatic(cmpInit, "DrawIndex", out int di)) { st.HaveDrawIndex = true; st.DrawIndex = di; }

                    // Gate (sequence) snapshot for camp. This often flips when the command menu is opened.
                    var seqType = FindTypeInLoadedAssemblies("Il2Cpp.dds3SequenceList");
                    int campGate = -999;
                    if (seqType != null && TryCallIntStaticMethod(seqType, "CheckCamp", out campGate))
                    {
                        st.HaveSeqGate = true;
                        st.GateCamp = campGate;
                    }

                    // Global work + seq info.
                    try
                    {
                        var gbwkMem = FindStaticMember(cmpInit, "CMP_GBWK");
                        if (gbwkMem != null)
                        {
                            var gbwkObj = GetStaticMemberValue(gbwkMem);
                            if (gbwkObj != null)
                            {
                                // gbFlag
                                var gbFlagProp = gbwkObj.GetType().GetProperty("Flag", BindingFlags.Public | BindingFlags.Instance);
                                if (gbFlagProp != null)
                                {
                                    object? v = gbFlagProp.GetValue(gbwkObj, null);
                                    if (v is sbyte sb)
                                    {
                                        st.HaveGbFlag = true;
                                        st.GbFlag = sb;
                                    }
                                }

                                // SeqInfo
                                var seqInfoProp = gbwkObj.GetType().GetProperty("SeqInfo", BindingFlags.Public | BindingFlags.Instance);
                                if (seqInfoProp != null)
                                {
                                    object? seqInfoObj = seqInfoProp.GetValue(gbwkObj, null);
                                    if (seqInfoObj != null)
                                    {
                                        // Read common sbyte fields.
                                        bool okAny = false;
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Current", out st.SeqCur);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Next", out st.SeqNext);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Last", out st.SeqLast);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Change", out st.SeqChange);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Flag", out st.SeqFlag);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "MesFlag", out st.SeqMesFlag);
                                        okAny |= TryReadSByteInstance(seqInfoObj, "Timer", out st.SeqTimer);
                                        st.HaveSeqInfo = okAny;
                                    }

                                // Derived cursor selections (RootCI/WorkCI).
                                try
                                {
                                    // RootCI[0] = main camp root menu cursor.
                                    object? rootArr = null;
                                    var rootProp = gbwkObj.GetType().GetProperty("RootCI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (rootProp != null)
                                    {
                                        rootArr = rootProp.GetValue(gbwkObj, null);
                                        if (rootArr != null && TryGetIndexedValue(rootArr, 0, out object? rootCi0) && rootCi0 != null)
                                        {
                                            if (TryReadCursorPosFromCursorInfo(rootCi0, out int rIdx, out int rShift, out int rList))
                                            {
                                                st.HaveRootCursor = true;
                                                st.RootIndex = rIdx;
                                                st.RootShift = rShift;
                                                st.RootListNums = rList;
                                                st.RootSel = rIdx + rShift;
                                            }
                                        }
                                    }

                                    // RootCI[] holds several independent cursors. We want the "active" secondary cursor
                                    // that corresponds to the currently selected root entry (RootCI[0]).
                                    // (Item/Party/Magatama/System each have their own submenu cursor.)
                                    try
                                    {
                                        int subCi = -1;
                                        CampSubCursorKind kind = CampSubCursorKind.None;

                                        if (st.HaveRootCursor)
                                        {
                                            switch (st.RootSel)
                                            {
                                                case 0: subCi = 1; kind = CampSubCursorKind.Item; break;      // Item
                                                case 2: subCi = 2; kind = CampSubCursorKind.Party; break;     // Party
                                                case 3: subCi = 3; kind = CampSubCursorKind.Hearts; break;    // Magatama
                                                case 5: subCi = 4; kind = CampSubCursorKind.System; break;    // System
                                                default: subCi = -1; kind = CampSubCursorKind.None; break;
                                            }
                                        }
                                        else
                                        {
                                            // Fallback: item submenu cursor is usually meaningful when rootSel is unknown.
                                            subCi = 1;
                                            kind = CampSubCursorKind.Item;
                                        }

                                        st.SubCiIndex = subCi;
                                        st.SubKind = kind;

                                        if (subCi >= 0 && rootArr != null && TryGetIndexedValue(rootArr, subCi, out object? subCiObj) && subCiObj != null)
                                        {
                                            if (TryReadCursorPosFromCursorInfo(subCiObj, out int sIdx, out int sShift, out int sList))
                                            {
                                                st.HaveSubCursor = true;
                                                st.SubIndex = sIdx;
                                                st.SubShift = sShift;
                                                st.SubListNums = sList;
                                                st.SubSel = sIdx + sShift;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // ignore
                                    }


                                    // WorkCI[0] = active work list cursor (e.g. item list).
                                    var workProp = gbwkObj.GetType().GetProperty("WorkCI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (workProp != null)
                                    {
                                        object? workArr = workProp.GetValue(gbwkObj, null);
                                        if (workArr != null && TryGetIndexedValue(workArr, 0, out object? workCi0) && workCi0 != null)
                                        {
                                            if (TryReadCursorPosFromCursorInfo(workCi0, out int wIdx, out int wShift, out int wList))
                                            {
                                                st.HaveWorkCursor = true;
                                                st.WorkIndex = wIdx;
                                                st.WorkShift = wShift;
                                                st.WorkListNums = wList;
                                                st.WorkSel = wIdx + wShift;
                                            }
                                        }
                                    }

                                    if (TryReadIntInstance(gbwkObj, "ItemCnt", out int itemCnt))
                                    {
                                        st.HaveItemCnt = true;
                                        st.ItemCnt = itemCnt;
                                    }

                                    if (st.HaveWorkCursor && st.HaveItemCnt && st.ItemCnt > 0)
                                    {
                                        int sel = st.WorkSel;
                                        if (sel >= 0 && sel < st.ItemCnt)
                                        {
                                            var itemIdxProp = gbwkObj.GetType().GetProperty("ItemIdx", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (itemIdxProp != null)
                                            {
                                                object? itemIdxArr = itemIdxProp.GetValue(gbwkObj, null);
                                                if (itemIdxArr != null && TryGetIndexedValue(itemIdxArr, sel, out object? itemIdObj) && itemIdObj != null)
                                                {
                                                    st.HaveItemSel = true;
                                                    st.ItemSelId = Convert.ToInt32(itemIdObj);
                                                    if (!TryGetItemName(st.ItemSelId, out string nm))
                                                        nm = "";
                                                    st.ItemSelName = nm;
                                                }
                                            }
                                        }
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
                    catch
                    {
                        // ignore
                    }

                    // Camp UI script state.
                    try
                    {
                        var campUiMem = FindStaticMember(cmpInit, "_campUIScr");
                        if (campUiMem != null)
                        {
                            var campUiObj = GetStaticMemberValue(campUiMem);
                            if (campUiObj != null)
                            {
                                st.HaveCampUI = true;

                                // Determine if the UI script (and its GameObject) is active.
                                bool active = false;
                                try
                                {
                                    // Prefer MonoBehaviour.gameObject.activeInHierarchy if available.
                                    if (campUiObj is Component c)
                                        active = c.gameObject != null && c.gameObject.activeInHierarchy;
                                    else
                                    {
                                        // Fallback: try a "gameObject" property.
                                        var goProp = campUiObj.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                        if (goProp != null)
                                        {
                                            object? goObj = goProp.GetValue(campUiObj, null);
                                            if (goObj is GameObject go)
                                                active = go.activeInHierarchy;
                                        }
                                    }
                                }
                                catch
                                {
                                    active = false;
                                }
                                st.CampUIActive = active;

                                // menuCur (GameObject) -> name only (for logging).
                                try
                                {
                                    var menuCurProp = campUiObj.GetType().GetProperty("menuCur", BindingFlags.Public | BindingFlags.Instance);
                                    if (menuCurProp != null)
                                    {
                                        object? curObj = menuCurProp.GetValue(campUiObj, null);
                                        if (curObj is GameObject go)
                                        {
                                            st.HaveMenuCurName = true;
                                            st.MenuCurName = go.name ?? "";
                                        }
                                        else if (curObj != null)
                                        {
                                            var nameProp = curObj.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (nameProp != null)
                                            {
                                                object? n = nameProp.GetValue(curObj, null);
                                                st.HaveMenuCurName = true;
                                                st.MenuCurName = n != null ? (n.ToString() ?? "") : "";
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // ignore
                                }
                                // UI active flags (root list vs item list etc).
                                try
                                {
                                    var menuListProp = campUiObj.GetType().GetProperty("menuListObj", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (menuListProp != null)
                                    {
                                        object? v = menuListProp.GetValue(campUiObj, null);
                                        if (v is GameObject go)
                                        {
                                            st.HaveMenuListActive = true;
                                            st.MenuListActive = go.activeInHierarchy;
                                        }
                                    }

                                    var itemListProp = campUiObj.GetType().GetProperty("itemListObj", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (itemListProp != null)
                                    {
                                        object? v = itemListProp.GetValue(campUiObj, null);
                                        if (v is GameObject go)
                                        {
                                            st.HaveItemListActive = true;
                                            st.ItemListActive = go.activeInHierarchy;
                                        }
                                    }
                                }
                                catch { /* ignore */ }

                                // menuObj slot count (campUI.menuObj): this is the physical UI slot array, typically larger than the logical root menu count.
                                try
                                {
                                    var menuObjProp = campUiObj.GetType().GetProperty("menuObj", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (menuObjProp != null)
                                    {
                                        object? menuArr = menuObjProp.GetValue(campUiObj, null);
                                        if (menuArr != null && TryReadIntInstance(menuArr, "Length", out int slots))
                                        {
                                            st.HaveMenuSlotLen = true;
                                            st.MenuSlotLen = slots;
                                        }
                                    }
                                }
                                catch { /* ignore */ }

                                // Logical root menu string table lengths from cmpInit (gCmpRootMenuStr/Sub).
                                object? rootStrArr = null;
                                object? rootSubStrArr = null;
                                object? itemRootStrArr = null;
                                object? partyRootStrArr = null;
                                object? heartsRootStrArr = null;
                                try
                                {
                                    var rootMem = FindStaticMember(cmpInit, "gCmpRootMenuStr");
                                    if (rootMem != null)
                                    {
                                        rootStrArr = GetStaticMemberValue(rootMem);
                                        if (rootStrArr != null && TryReadIntInstance(rootStrArr, "Length", out int rootLen))
                                        {
                                            st.HaveRootMenuLen = true;
                                            st.RootMenuLen = rootLen;
                                        }
                                    }
                                }
                                catch { /* ignore */ }

                                // Logical item-root submenu table (gCmpItemRootStr): Use / Discard / Gems / Key Items.
                                try
                                {
                                    var itemRootMem = FindStaticMember(cmpInit, "gCmpItemRootStr");
                                    if (itemRootMem != null)
                                        itemRootStrArr = GetStaticMemberValue(itemRootMem);
                                }
                                catch { /* ignore */ }

                                // Party root submenu table (gCmpPartyRootStr): Summon / Return to stock / Part with.
                                try
                                {
                                    var partyRootMem = FindStaticMember(cmpInit, "gCmpPartyRootStr");
                                    if (partyRootMem != null)
                                        partyRootStrArr = GetStaticMemberValue(partyRootMem);
                                }
                                catch { /* ignore */ }

                                // Hearts/Magatama submenu table (gCmpHeartsRootStr): Equip / Unequip.
                                try
                                {
                                    var heartsRootMem = FindStaticMember(cmpInit, "gCmpHeartsRootStr");
                                    if (heartsRootMem != null)
                                        heartsRootStrArr = GetStaticMemberValue(heartsRootMem);
                                }
                                catch { /* ignore */ }

                                try
                                {
                                    var subMem = FindStaticMember(cmpInit, "gCmpRootMenuSub");
                                    if (subMem != null)
                                    {
                                        object? subArr = GetStaticMemberValue(subMem);
                                        rootSubStrArr = subArr;
                                        if (subArr != null && TryReadIntInstance(subArr, "Length", out int subLen))
                                        {
                                            st.HaveRootSubLen = true;
                                            st.RootSubLen = subLen;
                                        }
                                    }
                                }
                                catch { /* ignore */ }


                                // Best-effort label for current selection (prefer derived root cursor selection).
                                try
                                {
                                    int selIdx = st.HaveRootCursor ? st.RootSel : (st.HaveSeqInfo ? st.SeqCur : -1);
                                    if (rootStrArr != null && selIdx >= 0 && st.HaveRootMenuLen && selIdx < st.RootMenuLen)
                                    {
                                        if (TryGetIl2CppStringArrayItem(rootStrArr, selIdx, out string label))
                                        {
                                            st.HaveSelectionLabel = true;
                                            st.SelectionLabel = label ?? "";
                                            if (TryLocalizeKey(st.SelectionLabel, out string loc))
                                            {
                                                st.HaveSelectionLocalized = true;
                                                st.SelectionLocalized = loc ?? "";
                                            }
                                        }
                                    }
                                }
                                catch { /* ignore */ }

                                // Best-effort label for active submenu selection (RootCI[k] against the matching string table).
                                try
                                {
                                    int subIdx = st.HaveSubCursor ? st.SubSel : -1;
                                    if (subIdx >= 0)
                                    {
                                        object? subArr = null;
                                        switch (st.SubKind)
                                        {
                                            case CampSubCursorKind.Item: subArr = itemRootStrArr; break;
                                            case CampSubCursorKind.Party: subArr = partyRootStrArr; break;
                                            case CampSubCursorKind.Hearts: subArr = heartsRootStrArr; break;
                                            case CampSubCursorKind.System: subArr = rootSubStrArr; break;
                                            default: subArr = null; break;
                                        }

                                        if (subArr != null && TryReadIntInstance(subArr, "Length", out int subLen) && subIdx < subLen)
                                        {
                                            if (TryGetIl2CppStringArrayItem(subArr, subIdx, out string subLabel))
                                            {
                                                st.HaveSubSelectionLabel = true;
                                                st.SubSelectionLabel = subLabel ?? "";
                                                if (TryLocalizeKey(st.SubSelectionLabel, out string subLoc))
                                                {
                                                    st.HaveSubSelectionLocalized = true;
                                                    st.SubSelectionLocalized = subLoc ?? "";
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { /* ignore */ }

                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryReadSByteInstance(object obj, string propName, out sbyte value)
            {
                value = 0;
                try
                {
                    var t = obj.GetType();
                    var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null)
                    {
                        object? v = p.GetValue(obj, null);
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte bb) { value = (sbyte)bb; return true; }
                        if (v is int i) { value = (sbyte)i; return true; }
                        if (v is short sh) { value = (sbyte)sh; return true; }
                    }

                    var f = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null)
                    {
                        object? v = f.GetValue(obj);
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte bb) { value = (sbyte)bb; return true; }
                        if (v is int i) { value = (sbyte)i; return true; }
                        if (v is short sh) { value = (sbyte)sh; return true; }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryReadIntInstance(object obj, string propName, out int value)
            {
                value = 0;
                try
                {
                    var t = obj.GetType();
                    var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null)
                    {
                        object? v = p.GetValue(obj, null);
                        if (v is int i) { value = i; return true; }
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte bb) { value = bb; return true; }
                        if (v is short sh) { value = sh; return true; }
                        if (v is ushort us) { value = us; return true; }
                    }

                    var f = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null)
                    {
                        object? v = f.GetValue(obj);
                        if (v is int i) { value = i; return true; }
                        if (v is sbyte sb) { value = sb; return true; }
                        if (v is byte bb) { value = bb; return true; }
                        if (v is short sh) { value = sh; return true; }
                        if (v is ushort us) { value = us; return true; }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryReadCursorPosFromCursorInfo(object cursorInfoObj, out int index, out int shift, out int listNums)
            {
                index = -1;
                shift = -1;
                listNums = -1;
                try
                {
                    var t = cursorInfoObj.GetType();
                    var p = t.GetProperty("CursorPos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p == null)
                        return false;

                    object? cp = p.GetValue(cursorInfoObj, null);
                    if (cp == null)
                        return false;

                    bool okIdx = TryReadIntInstance(cp, "Index", out index);
                    bool okShift = TryReadIntInstance(cp, "Shift", out shift);
                    bool okList = TryReadIntInstance(cp, "ListNums", out listNums);
                    return okIdx && okShift && okList;
                }
                catch
                {
                    return false;
                }
            }

            private static string FormatCampProbeLine(in CampProbeState st)
            {
                string gate = st.HaveSeqGate ? st.GateCamp.ToString() : "?";
                string ps = st.HaveProcStat ? st.ProcStat.ToString() : "?";
                string ex = st.HaveExitState ? st.ExitState.ToString() : "?";
                string gb = st.HaveGbFlag ? st.GbFlag.ToString() : "?";

                bool cmdAny = st.HaveCommandFlag && st.CommandFlagHash != 0;
                bool derivedOpen = (st.HaveCampUI && st.CampUIActive) || cmdAny || (st.HaveProcStat && st.ProcStat != 0) || (st.HaveExitState && st.ExitState != 0);
                string open = derivedOpen ? "OPEN" : "-";

                string seqInfo = st.HaveSeqInfo
                    ? $"cur={st.SeqCur} next={st.SeqNext} last={st.SeqLast} chg={st.SeqChange} flag={st.SeqFlag} mes={st.SeqMesFlag} t={st.SeqTimer}"
                    : "seqInfo=?";

                string cmd = st.HaveCommandFlag ? $"cmdFlag={st.CommandFlagLen}B {st.CommandFlagHex}" : "cmdFlag=?";
                string idx = $"idx[m={(st.HaveMenuIndex ? st.MenuIndex.ToString() : "?")},a={(st.HaveArrowIndex ? st.ArrowIndex.ToString() : "?")},i={(st.HaveItemIndex ? st.ItemIndex.ToString() : "?")},p={(st.HavePartyIndex ? st.PartyIndex.ToString() : "?")},s={(st.HaveStockIndex ? st.StockIndex.ToString() : "?")},d={(st.HaveDrawIndex ? st.DrawIndex.ToString() : "?")}]";

                string ui;
                if (st.HaveCampUI)
                {
                    string menuCurName = st.HaveMenuCurName ? (st.MenuCurName ?? "") : "?";
                    if (menuCurName.Length > 32)
                        menuCurName = menuCurName.Substring(0, 32);

                    string rootLen = st.HaveRootMenuLen ? st.RootMenuLen.ToString() : "?";
                    string subLen = st.HaveRootSubLen ? st.RootSubLen.ToString() : "?";
                    string slots = st.HaveMenuSlotLen ? st.MenuSlotLen.ToString() : "?";

                    string screen = "?";
                    if (st.HaveItemListActive && st.ItemListActive) screen = "ITEM";
                    else if (st.HaveMenuListActive && st.MenuListActive) screen = "ROOT";
                    else if (st.CampUIActive) screen = "UI";

                    string selKey = st.HaveSelectionLabel ? (st.SelectionLabel ?? "") : "";
                    string selLoc = st.HaveSelectionLocalized ? (st.SelectionLocalized ?? "") : "";
                    string selDisp = selKey;
                    if (!string.IsNullOrEmpty(selLoc) && selLoc != selKey)
                        selDisp = $"{selKey}->{selLoc}";
                    if (selDisp.Length > 48)
                        selDisp = selDisp.Substring(0, 48);

                    string subDisp = "";
                    if (st.HaveSubSelectionLabel)
                    {
                        string subKey = st.SubSelectionLabel ?? "";
                        string subLoc = st.HaveSubSelectionLocalized ? (st.SubSelectionLocalized ?? "") : "";
                        subDisp = subKey;
                        if (!string.IsNullOrEmpty(subLoc) && subLoc != subKey)
                            subDisp = $"{subKey}->{subLoc}";
                        if (subDisp.Length > 40)
                            subDisp = subDisp.Substring(0, 40);
                    }

                    string derived = "";
                    if (st.HaveRootCursor)
                        derived += $" rootSel={st.RootSel}({st.RootIndex}+{st.RootShift})/{st.RootListNums}";                    if (st.HaveSubCursor)
                    {
                        derived += $" subSel={st.SubSel}({st.SubIndex}+{st.SubShift})/{st.SubListNums}";
                        if (st.SubKind != CampSubCursorKind.None)
                            derived += $" subKind={st.SubKind.ToString().ToLowerInvariant()}";
                    }
                    if (st.HaveWorkCursor)
                        derived += $" workSel={st.WorkSel}({st.WorkIndex}+{st.WorkShift})/{st.WorkListNums}";
                    if (!string.IsNullOrEmpty(subDisp))
                        derived += $" sub=\"{subDisp}\"";

                    if (st.HaveItemSel)
                    {
                        string nm = st.ItemSelName ?? "";
                        if (nm.Length > 24)
                            nm = nm.Substring(0, 24);
                        derived += $" item={st.ItemSelId}({nm})";
                    }

                    // (root=submenu counts are from cmpInit string tables; slots are from campUI.menuObj)
                    // Keep quotes around sel/menuCur in the output; escape those quotes, but DO NOT escape the string
                    // literals inside the interpolation expression.
                    ui = $"ui={(st.CampUIActive ? "ON" : "OFF")} screen={screen} root={rootLen} sub={subLen} slots={slots}{derived} sel=\"{selDisp}\" menuCur=\"{menuCurName}\"";
                }
                else
                {
                    ui = "ui=?";
                }

                return $"campGate={gate}({open}) proc={ps} exit={ex} gbFlag={gb} {cmd} {idx} {seqInfo} {ui}";
            }

            // Hooked into DumpInputSnapshot for A/B diffs.
            private static void DumpCampProbeSnapshot(string tag)
            {
                try
                {
                    if (!TryGetCampProbeState(out var st) || !st.HaveCmpInit)
                    {
                        MelonLogger.Msg($"[Reimagined] campProbe: unavailable ({tag}).");
                        return;
                    }

                    MelonLogger.Msg($"[Reimagined] campProbe({tag}): {FormatCampProbeLine(in st)}");
                }
                catch
                {
                    // ignore
                }
            }

            // Hooked into seqTrace for open/close transitions.
            private static string TryGetCampProbeTraceSuffix(bool forceLog)
            {
                try
                {
                    if (!TryGetCampProbeState(out var st) || !st.HaveCmpInit)
                        return "";

                    int gate = st.HaveSeqGate ? st.GateCamp : int.MinValue;
                    int proc = st.HaveProcStat ? st.ProcStat : (sbyte)0;
                    int exit = st.HaveExitState ? st.ExitState : (sbyte)0;
                    int ui = st.HaveCampUI ? (st.CampUIActive ? 1 : 0) : -1;
                    int menuIdx = st.HaveRootCursor ? st.RootSel : (st.HaveMenuIndex ? st.MenuIndex : int.MinValue);
                    int subIdx = st.HaveSubCursor ? st.SubSel : int.MinValue;
                    int subKind = (int)st.SubKind;
                    int partyIdx = st.HavePartyIndex ? st.PartyIndex : int.MinValue;
                    int stockIdx = st.HaveStockIndex ? st.StockIndex : int.MinValue;
                    int arrowIdx = st.HaveArrowIndex ? st.ArrowIndex : int.MinValue;
                    int itemIdx = st.HaveWorkCursor ? st.WorkSel : (st.HaveItemIndex ? st.ItemIndex : int.MinValue);
                    int itemSelId = st.HaveItemSel ? st.ItemSelId : int.MinValue;
                    int cmdHash = st.HaveCommandFlag ? st.CommandFlagHash : 0;
                    int menuLen = st.HaveRootMenuLen ? st.RootMenuLen : -999;
                    int menuSlots = st.HaveMenuSlotLen ? st.MenuSlotLen : -999;
                    int screen = (st.HaveItemListActive && st.ItemListActive) ? 2 : (st.HaveMenuListActive && st.MenuListActive) ? 1 : (st.HaveCampUI && st.CampUIActive) ? 3 : 0;
                    int labelHash = st.HaveSelectionLabel ? (st.SelectionLabel ?? "").GetHashCode() : 0;
                    int subLabelHash = st.HaveSubSelectionLabel ? (st.SubSelectionLabel ?? "").GetHashCode() : 0;
                    string sel = st.HaveSelectionLabel ? (st.SelectionLabel ?? "") : "";
                    if (sel.Length > 16) sel = sel.Substring(0, 16);
                    int seqPack = st.HaveSeqInfo ? PackSeqInfo(st.SeqCur, st.SeqNext, st.SeqLast, st.SeqChange, st.SeqFlag, st.SeqMesFlag, st.SeqTimer) : int.MinValue;

                    bool changed =
                        gate != s_seqTraceLastCampGate ||
                        proc != s_seqTraceLastCmpProc ||
                        exit != s_seqTraceLastCmpExit ||
                        ui != s_seqTraceLastCampUIActive ||
                        menuIdx != s_seqTraceLastMenuIndex ||
                        subIdx != s_seqTraceLastSubIndex ||
                        subKind != s_seqTraceLastSubKind ||
                        partyIdx != s_seqTraceLastPartyIndex ||
                        stockIdx != s_seqTraceLastStockIndex ||
                        arrowIdx != s_seqTraceLastArrowIndex ||
                        itemIdx != s_seqTraceLastItemIndex ||
                        itemSelId != s_seqTraceLastItemSelId ||
                        cmdHash != s_seqTraceLastCmdHash ||
                        menuLen != s_seqTraceLastCampMenuLen ||
                        menuSlots != s_seqTraceLastCampMenuSlots ||
                        screen != s_seqTraceLastCampMenuScreen ||
                        labelHash != s_seqTraceLastCampMenuLabelHash ||
                        subLabelHash != s_seqTraceLastSubLabelHash ||
                        seqPack != s_seqTraceLastCampSeqPack;

                    if (!forceLog && !changed)
                        return "";

                    s_seqTraceLastCampGate = gate;
                    s_seqTraceLastCmpProc = proc;
                    s_seqTraceLastCmpExit = exit;
                    s_seqTraceLastCampUIActive = ui;
                    s_seqTraceLastMenuIndex = menuIdx;
                    s_seqTraceLastSubIndex = subIdx;
                    s_seqTraceLastSubKind = subKind;
                    s_seqTraceLastPartyIndex = partyIdx;
                    s_seqTraceLastStockIndex = stockIdx;
                    s_seqTraceLastArrowIndex = arrowIdx;
                    s_seqTraceLastItemIndex = itemIdx;
                    s_seqTraceLastItemSelId = itemSelId;
                    s_seqTraceLastCmdHash = cmdHash;
                    s_seqTraceLastCampMenuLen = menuLen;
                    s_seqTraceLastCampMenuSlots = menuSlots;
                    s_seqTraceLastCampMenuScreen = screen;
                    s_seqTraceLastCampMenuLabelHash = labelHash;
                    s_seqTraceLastSubLabelHash = subLabelHash;
                    s_seqTraceLastCampSeqPack = seqPack;

                    return "  " + FormatCampProbeLine(in st);
                }
                catch
                {
                    return "";
                }
            }


            private static bool TryGetIl2CppStringArrayItem(object arr, int index, out string value)
            {
                value = "";
                try
                {
                    var t = arr.GetType();

                    // Common pattern for Il2CppStringArray: get_Item(int) or an indexer named "Item".
                    var getItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (getItem != null)
                    {
                        object? v = getItem.Invoke(arr, new object[] { index });
                        if (v is string s) { value = s; return true; }
                        if (v != null) { value = v.ToString() ?? ""; return true; }
                        return false;
                    }

                    var idxer = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { typeof(int) }, null);
                    if (idxer != null)
                    {
                        object? v = idxer.GetValue(arr, new object[] { index });
                        if (v is string s) { value = s; return true; }
                        if (v != null) { value = v.ToString() ?? ""; return true; }
                        return false;
                    }

                    // Fallback: some array wrappers expose a GetValue(int).
                    var getValue = t.GetMethod("GetValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (getValue != null)
                    {
                        object? v = getValue.Invoke(arr, new object[] { index });
                        if (v is string s) { value = s; return true; }
                        if (v != null) { value = v.ToString() ?? ""; return true; }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryGetIl2CppByteArrayItem(object arr, int index, out byte value)
            {
                value = 0;
                try
                {
                    var t = arr.GetType();
                    var getItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (getItem != null)
                    {
                        object? v = getItem.Invoke(arr, new object[] { index });
                        if (v is byte b) { value = b; return true; }
                        if (v is sbyte sb) { value = unchecked((byte)sb); return true; }
                        if (v is int i) { value = unchecked((byte)i); return true; }
                        if (v is short sh) { value = unchecked((byte)sh); return true; }
                        if (v is ushort us) { value = unchecked((byte)us); return true; }
                        if (v is bool bb) { value = bb ? (byte)1 : (byte)0; return true; }
                        if (v != null) { value = Convert.ToByte(v); return true; }
                        return false;
                    }

                    var idxer = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { typeof(int) }, null);
                    if (idxer != null)
                    {
                        object? v = idxer.GetValue(arr, new object[] { index });
                        if (v is byte b) { value = b; return true; }
                        if (v != null) { value = Convert.ToByte(v); return true; }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryReadByteArrayHexStatic(Type t, string name, int sampleCount, out int length, out string hex, out int hash)
            {
                length = 0;
                hex = "";
                hash = 0;
                try
                {
                    var m = FindStaticMember(t, name);
                    if (m == null)
                        return false;

                    object? arr = GetStaticMemberValue(m);
                    if (arr == null)
                        return false;

                    if (!TryGetArrayLength(arr, out length))
                        length = -1;

                    int sample = sampleCount;
                    if (length >= 0)
                        sample = Math.Min(sample, length);
                    sample = Math.Max(sample, 0);

                    if (sample == 0)
                    {
                        hex = "";
                        hash = 0;
                        return true;
                    }

                    var sb = new System.Text.StringBuilder(sample * 3);
                    unchecked
                    {
                        int h = 0;
                        for (int i = 0; i < sample; i++)
                        {
                            if (i != 0) sb.Append(' ');
                            if (!TryGetIl2CppByteArrayItem(arr, i, out byte b))
                            {
                                sb.Append("??");
                                h = (h * 131) ^ (i * 17) ^ 0x5A;
                                continue;
                            }

                            sb.Append(b.ToString("X2"));
                            h = (h * 131) ^ b;
                        }

                        hex = sb.ToString();
                        hash = h;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}