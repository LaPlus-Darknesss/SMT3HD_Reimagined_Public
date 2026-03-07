#nullable enable
using System;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // Pass_B11/B12: Skill favorites (small, config-driven QoL)
            // =========================================================
            // Design intent:
            // - Reduce dependence on the native ReplacementPick flow for common testing.
            // - Keep this tiny and safe: still requires Skill_List + valid slot + resolved unit.
            // - Uses the same conservative swap/undo behavior as the candidate apply primitive.

            // NOTE: Keep this list small (12-30). These are purely devtool helpers.
            // B12 adds a file-backed favorites list, but we keep a hardcoded fallback
            // so DevTools still works even if the file is missing or malformed.
            private static readonly int[] s_skillFavoritesDefaults = new int[]
            {
                // Example utility/test skills (adjust to taste)
                26, // Megidola
                28, // Hama
                7,  // Bufu
                36, // Dia
                43, // Patra
                54, // Rakunda
                66, // Rakukaja
                67, // Makakaja
            };

            private static int s_skillFavoriteIndex;

            internal static bool TryGetCurrentSkillFavorite(out int skillId, out string skillName)
            {
                skillId = -1;
                skillName = string.Empty;

                try
                {
                    EnsureSkillFavoritesLoaded();
                    if (s_skillFavoritesActive.Length <= 0)
                        return false;

                    int idx = s_skillFavoriteIndex;
                    if (idx < 0) idx = 0;
                    if (idx >= s_skillFavoritesActive.Length) idx = s_skillFavoritesActive.Length - 1;

                    skillId = s_skillFavoritesActive[idx];
                    if (skillId >= 0 && TryGetSkillNameBestEffort(skillId, 0, out string nm))
                        skillName = ShowBlankName(nm);
                    else if (skillId >= 0)
                        skillName = $"<unknown:{skillId}>";

                    return skillId >= 0;
                }
                catch
                {
                    return false;
                }
            }

            internal static bool TryCycleSkillFavorite(int delta, out string summary)
            {
                summary = string.Empty;

                try
                {
                    EnsureSkillFavoritesLoaded();
                    if (s_skillFavoritesActive.Length <= 0)
                    {
                        summary = "favorite: <none>";
                        return false;
                    }

                    int n = s_skillFavoritesActive.Length;
                    int idx = s_skillFavoriteIndex;
                    idx = ((idx + delta) % n + n) % n;
                    s_skillFavoriteIndex = idx;

                    if (TryGetCurrentSkillFavorite(out int sid, out string nm))
                    {
                        summary = $"favorite: [{idx + 1}/{n}] {sid} \"{nm}\"";
                        return true;
                    }

                    summary = $"favorite: [{idx + 1}/{n}] <unresolved>";
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            internal static bool TryGetSkillFavoriteSummary(out string summary)
            {
                summary = string.Empty;
                try
                {
                    EnsureSkillFavoritesLoaded();
                    if (s_skillFavoritesActive.Length <= 0)
                        return false;

                    int idx = s_skillFavoriteIndex;
                    int n = s_skillFavoritesActive.Length;
                    if (TryGetCurrentSkillFavorite(out int sid, out string nm))
                    {
                        summary = $"[{idx + 1}/{n}] {sid} \"{nm}\"";
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }

            internal static bool TryApplyCurrentSkillFavoriteToSlot(out string summary, out string note)
            {
                summary = string.Empty;
                note = string.Empty;

                try
                {
                    if (!TryGetCurrentSkillFavorite(out int favId, out string favName) || favId < 0)
                    {
                        note = "favorite unavailable";
                        return false;
                    }

                    if (!TryGetNativeDebugMenuSkillEditorState(out var st))
                    {
                        note = "native skill state unavailable (open native debug menu → SKILL)";
                        return false;
                    }

                    if (st.Phase != NativeDebugMenuPhase.Skill_List)
                    {
                        note = $"wrong phase ({st.Phase}); enter Skill_List";
                        return false;
                    }

                    // Favorites apply is allowed in both SlotHighlight and ReplacementPick.
                    // SlotHighlight is the common case.
                    if (!st.HasUnit)
                    {
                        note = "no unit resolved (demon not selected?)";
                        return false;
                    }

                    if (!st.SlotValid || st.Slot < 0)
                    {
                        note = "invalid slot (gSelSkill out of range)";
                        return false;
                    }

                    int unitworkIndex = st.Unit.UnitworkIndex;
                    int unitId = st.Unit.UnitId;
                    int slot = st.Slot;

                    if (!TryGetDds3GlobalWorkObject(out object? gbwkObj) || gbwkObj == null)
                    {
                        note = "dds3GlobalWork unavailable";
                        return false;
                    }

                    if (!TryGetUnitworkObject(gbwkObj, unitworkIndex, out object? unitObj) || unitObj == null)
                    {
                        note = $"unitwork[{unitworkIndex}] unavailable";
                        return false;
                    }

                    if (!TryGetInstanceMemberValue(unitObj, "skill", out object? skillArrObj) || skillArrObj == null)
                    {
                        note = "unitwork.skill array unavailable";
                        return false;
                    }

                    if (!TryGetLengthOrCount(skillArrObj, out int arrLen, out _))
                    {
                        note = "unitwork.skill length unavailable";
                        return false;
                    }

                    if (slot < 0 || slot >= arrLen)
                    {
                        note = $"slot out of range (slot={slot} len={arrLen})";
                        return false;
                    }

                    // Snap before for event dump.
                    TryGetUnitSkillSnapshot(unitworkIndex, out var before);

                    if (!TryReadSkillIdFromArray(skillArrObj, slot, out int oldSlotSkill))
                    {
                        note = "failed to read current slot skill";
                        return false;
                    }

                    if (oldSlotSkill == favId)
                    {
                        summary = $"skill edit: favorite already in slot[{slot}] ({favName})";
                        s_lastSkillEditSummary = summary;
                        return true;
                    }

                    // Same conservative duplicate handling as candidate apply.
                    int foundAt = -1;
                    int scanN = Math.Min(8, arrLen);
                    for (int i = 0; i < scanN; i++)
                    {
                        if (i == slot)
                            continue;
                        if (TryReadSkillIdFromArray(skillArrObj, i, out int sid) && sid == favId)
                        {
                            foundAt = i;
                            break;
                        }
                    }

                    string op;
                    if (foundAt >= 0)
                    {
                        if (!TryWriteSkillIdToArray(skillArrObj, slot, favId) || !TryWriteSkillIdToArray(skillArrObj, foundAt, oldSlotSkill))
                        {
                            note = "write failed (swap)";
                            return false;
                        }

                        RecordNewSkillEdit(new NativeSkillEditRecord(true, unitworkIndex, unitId, slot, oldSlotSkill, favId, foundAt, favId, oldSlotSkill));
                        op = "favorite_swap";
                        summary = $"skill edit: favorite swap slot[{slot}] {oldSlotSkill}→{favId} ({favName}) with slot[{foundAt}]";
                    }
                    else
                    {
                        if (!TryWriteSkillIdToArray(skillArrObj, slot, favId))
                        {
                            note = "write failed";
                            return false;
                        }

                        RecordNewSkillEdit(new NativeSkillEditRecord(true, unitworkIndex, unitId, slot, oldSlotSkill, favId, -1, -1, -1));
                        op = "favorite_set";
                        summary = $"skill edit: favorite set slot[{slot}] {oldSlotSkill}→{favId} ({favName})";
                    }

                    s_lastSkillEditSummary = summary;
                    MelonLogger.Msg($"[Reimagined] {summary} (unitwork={unitworkIndex})");

                    // Snap after + emit a dedicated event file (analysis-grade artifact).
                    TryGetUnitSkillSnapshot(unitworkIndex, out var after);
                    EmitSkillEditEventFile(op, st, before, after, summary, note);

                    return true;
                }
                catch (Exception ex)
                {
                    note = "exception: " + ex.GetType().Name;
                    return false;
                }
            }
        }
    }
}
