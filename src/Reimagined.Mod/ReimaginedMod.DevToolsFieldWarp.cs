#nullable enable

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private void Devtools_ReloadWarpFavorites()
        {
            GameDebugMenuBridge.ReloadWarpFavorites();
        }




private void Devtools_NormalizeWarpFavorites()
{
    GameDebugMenuBridge.NormalizeWarpFavoritesFile();
}

        private void Devtools_AppendCurrentHitDoorToWarpFavorites()
        {
            GameDebugMenuBridge.AppendCurrentHitDoorToWarpFavorites();
        }


        private void Devtools_ToggleWarpFavoritesFilterByContext()
        {
            GameDebugMenuBridge.ToggleWarpFavoritesFilterByContext();
        }

        private void Devtools_WarpFavoritePrev()
        {
            GameDebugMenuBridge.WarpFavoritePrev();
        }

        private void Devtools_WarpFavoriteNext()
        {
            GameDebugMenuBridge.WarpFavoriteNext();
        }

        private void Devtools_WarpToFavoriteConfirm()
        {
            GameDebugMenuBridge.WarpToFavoriteConfirm();
        }

        private void Devtools_DumpFieldWarpTable()
        {
            GameDebugMenuBridge.DumpFieldWarpTableAndContext();
        }
    }
}