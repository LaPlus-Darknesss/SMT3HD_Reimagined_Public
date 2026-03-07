#nullable enable
using System;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        // =========================================================
        // Pass_B11: Skill favorites (hotkey wrappers)
        // =========================================================
        // Tiny wrappers that:
        //  - cycle a small curated list of skill IDs
        //  - apply the current favorite to the currently selected slot
        //  - reuse the same undo buffer as the candidate primitive

        private void Devtools_SkillFavoritePrev()
        {
            try
            {
                if (GameDebugMenuBridge.TryCycleSkillFavorite(-1, out string summary))
                {
                    Toast(summary, ToastKind.Ok);
                }
                else
                {
                    Toast("favorite: unavailable", ToastKind.Warn);
                }
            }
            catch (Exception ex)
            {
                Toast("favorite: exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] favorite prev exception: " + ex.GetType().Name);
            }
        }

        private void Devtools_SkillFavoriteNext()
        {
            try
            {
                if (GameDebugMenuBridge.TryCycleSkillFavorite(+1, out string summary))
                {
                    Toast(summary, ToastKind.Ok);
                }
                else
                {
                    Toast("favorite: unavailable", ToastKind.Warn);
                }
            }
            catch (Exception ex)
            {
                Toast("favorite: exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] favorite next exception: " + ex.GetType().Name);
            }
        }

        private void Devtools_ApplySkillFavoriteToSlot()
        {
            try
            {
                if (GameDebugMenuBridge.TryApplyCurrentSkillFavoriteToSlot(out string summary, out string note))
                {
                    if (!string.IsNullOrEmpty(summary))
                        Toast(summary, ToastKind.Ok);
                    else
                        Toast("favorite applied", ToastKind.Ok);
                }
                else
                {
                    string msg = string.IsNullOrEmpty(note) ? "favorite apply failed" : note;
                    Toast(msg, ToastKind.Warn);
                    MelonLogger.Warning("[Reimagined] favorite apply failed: " + msg);
                }
            }
            catch (Exception ex)
            {
                Toast("favorite apply exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] favorite apply exception: " + ex.GetType().Name);
            }
        }

        private void Devtools_ReloadSkillFavorites()
        {
            try
            {
                bool ok = GameDebugMenuBridge.TryReloadSkillFavorites(out string summary, out string note);
                string msg = !string.IsNullOrEmpty(summary) ? summary : (ok ? "skill favorites reloaded" : "skill favorites reload failed");
                if (!string.IsNullOrEmpty(note))
                    msg += " · " + note;

                Toast(msg, ok ? ToastKind.Ok : ToastKind.Warn);
                MelonLogger.Msg("[Reimagined] " + msg);
            }
            catch (Exception ex)
            {
                Toast("skill favorites reload exception", ToastKind.Error);
                MelonLogger.Warning("[Reimagined] skill favorites reload exception: " + ex.GetType().Name);
            }
        }
    }
}
