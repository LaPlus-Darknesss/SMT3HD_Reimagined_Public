#nullable enable
using System;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        // =========================================================
        // Hotkey handlers for the native-skill edit primitive
        // =========================================================


        private void Devtools_ApplyNativeSkillCandidateToSlot()
        {
            try
            {
                if (GameDebugMenuBridge.TryApplyNativeSkillCandidateToSlot(out string summary, out string note))
                {
                    if (!string.IsNullOrEmpty(summary))
                        Toast(summary, ToastKind.Ok);
                    else
                        Toast("Skill edit applied", ToastKind.Ok);
                }
                else
                {
                    string msg = string.IsNullOrEmpty(note) ? "Skill edit apply failed" : note;
                    Toast(msg, ToastKind.Warn);
                    MelonLogger.Warning("[Reimagined] Skill edit apply failed: " + msg);
                }
            }
            catch (Exception ex)
            {
                Toast("Skill edit apply exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] Skill edit apply exception: " + ex.GetType().Name);
            }
        }

        private void Devtools_UndoLastNativeSkillEdit()
        {
            try
            {
                if (GameDebugMenuBridge.TryUndoLastNativeSkillEdit(out string summary, out string note))
                {
                    if (!string.IsNullOrEmpty(summary))
                        Toast(summary, ToastKind.Ok);
                    else
                        Toast("Skill edit undone", ToastKind.Ok);
                }
                else
                {
                    string msg = string.IsNullOrEmpty(note) ? "Skill edit undo failed" : note;
                    Toast(msg, ToastKind.Warn);
                    MelonLogger.Warning("[Reimagined] Skill edit undo failed: " + msg);
                }
            }
            catch (Exception ex)
            {
                Toast("Skill edit undo exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] Skill edit undo exception: " + ex.GetType().Name);
            }
        }


        private void Devtools_RedoLastNativeSkillEdit()
        {
            try
            {
                if (GameDebugMenuBridge.TryRedoLastNativeSkillEdit(out string summary, out string note))
                {
                    if (!string.IsNullOrEmpty(summary))
                        Toast(summary, ToastKind.Ok);
                    else
                        Toast("Skill edit redone", ToastKind.Ok);
                }
                else
                {
                    string msg = string.IsNullOrEmpty(note) ? "Skill edit redo failed" : note;
                    Toast(msg, ToastKind.Warn);
                    MelonLogger.Warning("[Reimagined] Skill edit redo failed: " + msg);
                }
            }
            catch (Exception ex)
            {
                Toast("Skill edit redo exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] Skill edit redo exception: " + ex.GetType().Name);
            }
        }
    }
}
