#nullable enable
using System;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // NativeDebugMenuSelectionSnapshot (immutable POD)
            // =========================================================


            internal enum NativeDebugMenuPhase
            {
                Unknown = 0,
                Root = 1,            // Not inside an active subflow; root cursor still tells us which branch is highlighted.
                Skill_DemonSelect = 2,
                Skill_List = 3,
            }

            internal enum SkillDemonSlotMappingKind
            {
                Unknown = 0,
                // meaning stocklist includes the protagonist as its first element.
                Stocklist0IsProtag = 1,
                // Legacy fallback: protagonist is implicit (unitwork[0]) and stocklist begins at row 1.
                ProtagPlusStocklistMinus1 = 2,
            }

            internal readonly struct NativeDebugMenuSelectionSnapshot
            {
                public readonly NativeDebugMenuPhase Phase;

                public readonly int gActive1;
                public readonly int gActive2;
                public readonly int gSelSkill;
                public readonly int gCursorSection;

                public readonly int ActiveCursorIdx;
                public readonly int ListNums;
                public readonly int ShiftMax;
                public readonly int Index;
                public readonly int Shift;
                public readonly int Sel;

                public readonly string RootWord;      // e.g. "SKILL", "ITEM"
                public readonly bool RootWordKnown;

                // Demon selection (SKILL branch)
                public readonly bool HasUnit;
                public readonly int UnitRow;          // row index (0..8) in the demon list
                public readonly int UnitStockIndex;   // stocklist index used to resolve (or -1 if not applicable)
                public readonly SkillDemonSlotMappingKind UnitMappingKind;
                public readonly UnitResolveInfo Unit; // valid iff HasUnit

                // Skill highlight (SKILL list phase)
                public readonly bool HasSkill;
                public readonly int SkillId;
                public readonly string SkillName;

                public NativeDebugMenuSelectionSnapshot(
                    NativeDebugMenuPhase phase,
                    int gActive1,
                    int gActive2,
                    int gSelSkill,
                    int gCursorSection,
                    int activeCursorIdx,
                    int listNums,
                    int shiftMax,
                    int index,
                    int shift,
                    int sel,
                    string rootWord,
                    bool rootWordKnown,
                    bool hasUnit,
                    int unitRow,
                    int unitStockIndex,
                    SkillDemonSlotMappingKind mappingKind,
                    UnitResolveInfo unit,
                    bool hasSkill,
                    int skillId,
                    string skillName)
                {
                    Phase = phase;

                    this.gActive1 = gActive1;
                    this.gActive2 = gActive2;
                    this.gSelSkill = gSelSkill;
                    this.gCursorSection = gCursorSection;

                    ActiveCursorIdx = activeCursorIdx;
                    ListNums = listNums;
                    ShiftMax = shiftMax;
                    Index = index;
                    Shift = shift;
                    Sel = sel;

                    RootWord = rootWord ?? string.Empty;
                    RootWordKnown = rootWordKnown;

                    HasUnit = hasUnit;
                    UnitRow = unitRow;
                    UnitStockIndex = unitStockIndex;
                    UnitMappingKind = mappingKind;
                    Unit = unit;

                    HasSkill = hasSkill;
                    SkillId = skillId;
                    SkillName = skillName ?? string.Empty;
                }
            }
        }
    }
}
