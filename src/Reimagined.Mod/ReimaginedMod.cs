#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[assembly: MelonInfo(typeof(SMT3HD_Reimagined.ReimaginedMod), "SMT3HD Reimagined", "0.0.15", "Noelle")]
[assembly: MelonGame("アトラス", "smt3hd")]

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod : MelonMod
    {
        // ---- Project paths (outside Steam folder) ----
        private static readonly string ProjectRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SMT3HD_Reimagined");

        private static readonly string DumpsDir = Path.Combine(ProjectRoot, "dumps");
        private static readonly string LogsDir = Path.Combine(ProjectRoot, "logs", "reimagined");

        // Monotonic nonce used by MakeDumpPath() to avoid overwrite when hotkeys are pressed rapidly.
        private static int s_dumpNonce = 0;


        private static string MakeDumpPath(string stem, string ext = "txt")
        {
            // Centralized dump path helper so new dumpers don't re-implement timestamp formatting differently.
            // Always writes to ProjectRoot/dumps and ensures the directory exists.
            //
            // IMPORTANT: use millisecond precision + a monotonic nonce so rapid hotkey presses never overwrite dumps.
            Directory.CreateDirectory(DumpsDir);

            int nonce = Interlocked.Increment(ref s_dumpNonce);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return Path.Combine(DumpsDir, $"{stem}_{ts}_{nonce:D4}.{ext}");
        }


        // ---- Simple schema for jsonl ----
        private const int SchemaVersion = 1;

        // ---- Runtime state ----
        private bool _overlayOpen = false;         // DevTools overlay open
        private bool _pauseWhenOpen = false;
        private bool _captureInputsWhenOpen = false;
        private bool _allowTerminalInvokes = false; // safety gate: invoking Il2Cpp.* methods can have side-effects
#pragma warning disable CS0169, CS0414
        private float _overlayScale = 1.0f;

        // IMGUI overlay state (bootstrap UI; later we can migrate to UGUI per docs/OverlaySpec)
        private Rect _overlayRect = new Rect(16, 16, 720, 560);
        private int _overlayTab = 0;
        private Vector2 _scrollStatus;
        private Vector2 _scrollDump;
        private Vector2 _scrollWatch;
        private Vector2 _scrollLogs;
        private Vector2 _scrollSettings;
#pragma warning restore CS0169, CS0414
        private string? _lastDumpPath;
        private DateTime _nextUiWatchPoll = DateTime.MinValue;
        private float _lastShiftHeldUnscaled = -999f; // hotkey debouncing: shift held time (unscaled)
        private float _lastShiftDownUnscaled = -999f; // hotkey debouncing: shift down edge (unscaled)
        private Dictionary<string, UiNodeState>? _uiWatchCache;
        private string? _uiWatchCacheSig;
        private DateTime _uiWatchCacheTs;

        private bool _cursorCaptured = false;
        private bool _prevCursorVisible;
        private CursorLockMode _prevCursorLockState;

        private const int OverlayLogMax = 240;
        private readonly List<OverlayLogLine> _overlayLog = new List<OverlayLogLine>(OverlayLogMax);
        private readonly List<OverlayToast> _toasts = new List<OverlayToast>(8);

        private float _prevTimeScale = 1.0f;
        private bool _pauseApplied = false;
        private Delegate? _sceneLoadedHandler;

        // UGUI overlay root (IMGUI is not available in this game's Unity bindings)
        private DevToolsOverlayUGUI? _uguiOverlay;

        private DateTime _uguiOverlayRetryAfter = DateTime.MinValue;
        private int _uguiOverlayCreateFailures = 0;
        private string? _uguiOverlayLastCreateError;

        private StreamWriter? _jsonl;


        // ---- UI snapshot (for delta/watchlist tools) ----
        private static Dictionary<string, UiNodeState>? _lastUiSnapshot;
        private static DateTime _lastUiSnapshotTs;
        private static string? _lastUiSnapshotSig;

        private readonly struct UiNodeState
        {
            public readonly bool ActiveSelf;
            public readonly bool ActiveHier;

            public UiNodeState(bool activeSelf, bool activeHier)
            {
                ActiveSelf = activeSelf;
                ActiveHier = activeHier;
            }

            public override string ToString() => $"activeSelf={ActiveSelf} activeHier={ActiveHier}";
        }

        private readonly struct OverlayLogLine
        {
            public readonly DateTime Ts;
            public readonly string Category;
            public readonly string Message;

            public OverlayLogLine(DateTime ts, string category, string message)
            {
                Ts = ts;
                Category = category;
                Message = message;
            }
        }

        private enum ToastKind
        {
            Info,
            Ok,
            Warn,
            Error,
        }

        private readonly struct OverlayToast
        {
            public readonly DateTime Until;
            public readonly ToastKind Kind;
            public readonly string Message;

            public OverlayToast(DateTime until, ToastKind kind, string message)
            {
                Until = until;
                Kind = kind;
                Message = message;
            }
        }

        private static readonly string[] UiWatchPaths =
        {
            // --- Global / common ---
            "Canvas_UI",
            "Canvas_UI/fieldUI",
            "Canvas_UI/fieldUI/flocation",
            "Canvas_UI/fieldUI/moon",
            "Canvas_UI/campUIBase",
            "Canvas_UI/campUIBase/campUI",
            "Canvas_UI/campUIBase/camp_bg",
            "Canvas_UI/campUIBase/campUI/menu",
            "Canvas_UI/buttonguide01",
            "Canvas_UI/buttonguide01/guide",
            "EventSystem",
            "UI Camera",
            "Main Canvas",
            "RenderCanvas",

            // --- Shop ---
            "Canvas_UI/shopUI",
            "Canvas_UI/shopUI/institutionUI",
            "Canvas_UI/shopUI/shopbase",
            "Canvas_UI/shopUI/shopbgcolor",
            "Canvas_UI/shopUI/shophelp",
            "Canvas_UI/shopUI/shoplist",

            // Root-level variants (sometimes exposed as separate roots)
            "shopUI",
            "shopUI/institutionUI",
            "shopUI/shopbase",
            "shopUI/shopbgcolor",
            "shopUI/shophelp",
            "shopUI/shoplist",

            // --- Save terminal ---
            "Canvas_UI/terminalUI",
            "Canvas_UI/terminalUI/institutionUI",
            "Canvas_UI/terminalUI/menu_tmnl",
            "Canvas_UI/terminalUI/msg_tmnl",
            "Canvas_UI/terminalUI/msg_tmnl_bg",
            "Canvas_UI/terminalUI/tmnl_select",
            "Canvas_UI/terminalUI/common_bg",
            "Canvas_UI/terminalUI/cmd_base",
            "Canvas_UI/terminalUI/cmd_panel",

            // Root-level variants
            "terminalUI",
            "terminalUI/institutionUI",
            "terminalUI/menu_tmnl",
            "terminalUI/msg_tmnl",
            "terminalUI/msg_tmnl_bg",
            "terminalUI/tmnl_select",
            "terminalUI/common_bg",
            "terminalUI/cmd_base",
            "terminalUI/cmd_panel",

            // --- Save UI (observed as a clone under Canvas_UI) ---
            "Canvas_UI/saveUI(Clone)",
            "Canvas_UI/saveUI(Clone)/savelist",
            "Canvas_UI/saveUI(Clone)/top",
            "Canvas_UI/saveUI(Clone)/cmnBg",
            "saveUI",
            "SaveUI",
        };

        public override void OnInitializeMelon()
        {
            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(DumpsDir);
            Directory.CreateDirectory(LogsDir);

            var jsonlPath = Path.Combine(LogsDir, $"reimagined_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
            _jsonl = new StreamWriter(jsonlPath, append: true, encoding: new UTF8Encoding(false)) { AutoFlush = true };

            MelonLogger.Msg("[Reimagined] Initialized (P0 devtools-first; UGUI overlay + scene/UI probes).");
            LogJson("boot", "initialized", new Dictionary<string, object?>
            {
                ["project_root"] = ProjectRoot,
                ["dumps_dir"] = DumpsDir,
                ["logs_dir"] = LogsDir,
                ["unity"] = Application.unityVersion,
                ["app_version"] = Application.version,
                ["pause_when_open_default"] = _pauseWhenOpen
            });

            TryHookSceneLoaded(true);
        }

        public override void OnDeinitializeMelon()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                TryHookSceneLoaded(false);
            }
            catch { /* ignore */ }

            try { GameDebugMenuBridge.ForceCleanup("mod_deinit"); } catch { /* ignore */ }

            try { _jsonl?.Dispose(); } catch { /* ignore */ }
            _jsonl = null;
        }


        private void TryHookSceneLoaded(bool enable)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var ev = typeof(SceneManager).GetEvent("sceneLoaded", BindingFlags.Public | BindingFlags.Static);
                if (ev == null || ev.EventHandlerType == null)
                {
                    if (enable)
                        MelonLogger.Warning("[Reimagined] SceneManager.sceneLoaded not available in current bindings; using polling only.");
                    return;
                }

                if (_sceneLoadedHandler == null)
                    _sceneLoadedHandler = Delegate.CreateDelegate(ev.EventHandlerType, this, nameof(OnSceneLoaded));

                if (enable)
                    ev.AddEventHandler(null, _sceneLoadedHandler);
                else
                    ev.RemoveEventHandler(null, _sceneLoadedHandler);
            }
            catch (Exception ex)
            {
                if (enable)
                    MelonLogger.Warning($"[Reimagined] Failed to hook sceneLoaded: {ex.GetType().Name}: {ex.Message}");
            }
        }

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MelonLogger.Msg($"[Reimagined] SceneLoaded: name='{scene.name}', buildIndex={scene.buildIndex}, mode={mode}");
            LogJson("scene", "loaded", new Dictionary<string, object?>
            {
                ["name"] = scene.name,
                ["buildIndex"] = scene.buildIndex,
                ["mode"] = mode.ToString()
            });
        }

        public override void OnUpdate()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Hotkeys:
                // F1 = toggle game-native debug menu (cmpTest)
                // Shift+F1 = toggle fallback overlay (UGUI)
                // F2 = dump scene UI probe (full hierarchy; big)
                // F4 = dump capabilities (reflection surface / API sanity)
                // F5 = dump UI signature summary (fast heuristics)
                // F6 = dump UI delta since last F6 (active state changes; very useful)
                // F7 = dump UI watchlist (targeted important nodes)
                // F8 = clear UI snapshot (forces next F6 to write baseline)
                // F9 = dump UI anchor components (focused component type listing for key nodes)
                // F10 = dump reflection surface (best-effort)
                // F11 = dump UI component inventory
                // F12 = dump UI controller state
                // F3 = dump terminal seam state (invoke-gated; see overlay settings)

                // Track Shift edges and held time using unscaled time so it works even if the game/menu pauses time.
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                    _lastShiftDownUnscaled = Time.unscaledTime;

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    _lastShiftHeldUnscaled = Time.unscaledTime;

                // Failsafe: if we accidentally "nuke" Unity input via ResetInputAxes, we still want
                // a way to recover without rebooting the game. This hotkey is separate from snapshots.
                bool _ctrlHeldNow = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool _shiftHeldNow = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (_ctrlHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F2))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Shift+F2 (failsafe: force-disable debug-menu input-block ResetAxes)");
                    GameDebugMenuBridge.ForceDisableInputBlockResetAxes("failsafe_hotkey");
                    return;
                }

                // Skill editor primitive (safe, incremental): apply currently highlighted replacement candidate
                // to the currently selected skill slot (native debug menu SKILL flow), and allow a single-step undo.
                // We intentionally use Ctrl+Alt+Shift + a letter key to avoid colliding with existing Ctrl+Alt+Fn binds.
                bool _altHeldNow = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.K))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+K (native skill apply primitive)");
                    Devtools_ApplyNativeSkillCandidateToSlot();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.U))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+U (native skill undo primitive)");
                    Devtools_UndoLastNativeSkillEdit();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Y))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+Y (native skill redo)");
                    Devtools_RedoLastNativeSkillEdit();
                    return;
                }

                // Skill favorites (Pass_B11): cycle + apply without entering the native ReplacementPick flow.
                // These still require you to be inside the native debug menu SKILL list with a valid slot.
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.H))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+H (skill favorite prev)");
                    Devtools_SkillFavoritePrev();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.L))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+L (skill favorite next)");
                    Devtools_SkillFavoriteNext();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.G))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+G (apply current skill favorite to slot)");
                    Devtools_ApplySkillFavoriteToSlot();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.R))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+R (reload skill favorites file)");
                    Devtools_ReloadSkillFavorites();
                    return;
                }

                // Field warp / map tools (Pass_B14): map/warp identity + table dump, and a safe warp primitive.
                // We keep this incremental: first we build rich dumpers so mapping is cheap, then we layer UX.
                //
                // Hotkeys:
                //   Ctrl+Alt+Shift+F9 : dump field warp table + current field context
                //   Ctrl+Alt+Shift+F7 : reload warp favorites file
				//   Ctrl+Alt+Shift+T  : toggle warp trace (logs state changes during real door transitions)
                //   Ctrl+Alt+Shift+[  : warp favorite prev (also LeftArrow)
                //   Ctrl+Alt+Shift+]  : warp favorite next (also RightArrow)
                //   Ctrl+Alt+Shift+F8 : warp to current favorite (double-tap confirm)
                // Legacy aliases kept for muscle memory / keyboard layouts:
                //   Ctrl+Alt+Shift+O  : reload warp favorites file
                //   Ctrl+Alt+Shift+B  : warp favorite prev
                //   Ctrl+Alt+Shift+N  : warp favorite next
                //   Ctrl+Alt+Shift+M  : warp to current favorite (double-tap confirm)
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F9))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F9 (dump field warp table + map context)");
                    Devtools_DumpFieldWarpTable();
                    return;
                }


if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F4))
{
    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F4 (normalize warp favorites file)");
    Devtools_NormalizeWarpFavorites();
    return;
}


                
                
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F5))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F5 (toggle warp favorites filter-by-context)");
                    Devtools_ToggleWarpFavoritesFilterByContext();
                    return;
                }

if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F6))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F6 (append current hit door to warp favorites file)");
                    Devtools_AppendCurrentHitDoorToWarpFavorites();
                    return;
                }

if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F7))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F7 (reload warp favorites file)");
                    Devtools_ReloadWarpFavorites();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.LeftArrow)))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+[ / LeftArrow (warp favorite prev)");
                    Devtools_WarpFavoritePrev();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.RightArrow)))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+] / RightArrow (warp favorite next)");
                    Devtools_WarpFavoriteNext();
                    return;
                }
            if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.P))
            {
                // Ctrl+Alt+Shift+P: arm/cancel recording of the next field transition into dumps/warp_catalog.jsonl
                GameDebugMenuBridge.HotkeyToggleWarpCatalogRecord();
                return;
            }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.J))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+J (dump warp catalog summary)");
                    GameDebugMenuBridge.HotkeyDumpWarpCatalogSummary();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.I))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+I (dump selected warp catalog route)");
                    GameDebugMenuBridge.HotkeyDumpWarpCatalogSelectedRoute();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.V))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+V (dump selected route as warp favorite snippet/template)");
                    GameDebugMenuBridge.HotkeyDumpWarpCatalogSelectedFavoriteSnippet();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.A))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+A (append selected door route to warp favorites)");
                    GameDebugMenuBridge.HotkeyAppendSelectedWarpCatalogDoorToFavorites();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.C))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+C (save selected catalog route to route favorites registry)");
                    GameDebugMenuBridge.HotkeySaveSelectedWarpCatalogRouteFavorite();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Semicolon))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+; (route-favorites browse prev)");
                    GameDebugMenuBridge.HotkeyWarpCatalogRouteFavoritesBrowsePrev();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Quote))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+' (route-favorites browse next)");
                    GameDebugMenuBridge.HotkeyWarpCatalogRouteFavoritesBrowseNext();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Backslash))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+\\ (dump selected route-favorite)");
                    GameDebugMenuBridge.HotkeyDumpSelectedWarpCatalogRouteFavorite();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Slash))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+/ (toggle warp catalog browse mode)");
                    GameDebugMenuBridge.HotkeyWarpCatalogBrowseToggleMode();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Comma))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+, (warp catalog browse prev)");
                    GameDebugMenuBridge.HotkeyWarpCatalogBrowsePrev();
                    return;
                }

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.Period))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+. (warp catalog browse next)");
                    GameDebugMenuBridge.HotkeyWarpCatalogBrowseNext();
                    return;
                }

if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.F8))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F8 (warp to current favorite; double-tap confirm)");
                    Devtools_WarpToFavoriteConfirm();
                    return;
                }

				if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.T))
				{
					MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+T (toggle warp trace)");
					GameDebugMenuBridge.HotkeyToggleFieldWarpTrace();
					return;
				}

                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.O))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+O (reload warp favorites file)");
                    Devtools_ReloadWarpFavorites();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.B))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+B (warp favorite prev)");
                    Devtools_WarpFavoritePrev();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.N))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+N (warp favorite next)");
                    Devtools_WarpFavoriteNext();
                    return;
                }
                if (_ctrlHeldNow && _altHeldNow && _shiftHeldNow && Input.GetKeyDown(KeyCode.M))
                {
                    MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+M (warp to current favorite; double-tap confirm)");
                    Devtools_WarpToFavoriteConfirm();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.F1))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F1: toggle live camp highlight trace (devtools).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F1 (toggle live camp highlight trace)");
                        GameDebugMenuBridge.HotkeyToggleCampHighlightTrace();
                        return;
                    }


                    // Default: open the game-native debug menu surface (cmpTest) so we can stop fighting overlay quirks.
                    // Shift+F1 toggles the legacy overlay (fallback). We allow a small timing window so "Shift then F1"
                    // and "F1 then Shift" within a short interval still counts as Shift+F1.
                    bool ctrlHeldNow = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                    if (ctrlHeldNow)
                    {
                        bool altHeldNow = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                        if (altHeldNow)
                        {
                            MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F1 (cycle debug-menu input-capture strategy)");
                            GameDebugMenuBridge.HotkeyCyclePauseCaptureStrategy();
                            return;
                        }

                        bool shiftHeldNow2 = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        if (shiftHeldNow2)
                        {
                            MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Shift+F1 (toggle debug-menu input-block ResetAxes)");
                            GameDebugMenuBridge.HotkeyToggleInputBlockResetAxes();
                        }
                        else
                        {
                            MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+F1 (toggle debug-menu input-capture)");
                            GameDebugMenuBridge.HotkeyTogglePauseCapture();
                        }
                        return;
                    }

                    bool shiftHeldNow = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    bool shiftRecent = (Time.unscaledTime - _lastShiftHeldUnscaled) <= 0.15f || (Time.unscaledTime - _lastShiftDownUnscaled) <= 0.15f;
                    bool shiftCombo = shiftHeldNow || shiftRecent;

                    if (shiftCombo)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Shift+F1 (toggle DevTools overlay)");
                        ToggleDevtoolsMode();
                    }
                    else
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: F1 (toggle game debug menu)");
                        OpenGameDebugMenu();
                    }
                }

                if (Input.GetKeyDown(KeyCode.F2))
{
    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    // Ctrl+Alt+F2: flip fldPlayerEventStop polarity (FieldPlayerStop strategy debugging)
    if (ctrlHeld && altHeld)
    {
        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F2 (toggle field-stop polarity)");
        GameDebugMenuBridge.HotkeyToggleFieldStopPolarity();
        return;
    }

    if (ctrlHeld)
        GameDebugMenuBridge.HotkeyDumpInputSnapshot();
    else
        DumpSceneUiProbe();
}
if (Input.GetKeyDown(KeyCode.F4))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F4: toggle padmap trace (logs TRIG2/PRESS2 when keys are pressed; useful for mapping leak sources).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F4 (toggle padmap trace)");
                        GameDebugMenuBridge.HotkeyTogglePadmapTrace();
                        return;
                    }

                    DumpCapabilities();
                }

                if (Input.GetKeyDown(KeyCode.F5))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F5: toggle command-menu style PAD_DISABLE helper (vA14).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F5 (toggle Cmd PAD_DISABLE)");
                        GameDebugMenuBridge.HotkeyToggleCmdPadDisable();
                        return;
                    }

                    DumpUiSignatureSummary();
                }
            
                if (Input.GetKeyDown(KeyCode.F8))
{
    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    // Ctrl+Alt+F8: toggle fldPlayer input-scrub overlay for FieldUnitPadRelease strategy (vA20).
    if (ctrlHeld && altHeld)
    {
        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F8 (toggle fldPlayer input-scrub overlay for FieldUnitPadRelease)");
        GameDebugMenuBridge.HotkeyToggleUnitPadFieldPlayerInputScrubOverlay();
        return;
    }

    ClearUiSnapshots();
}


                if (Input.GetKeyDown(KeyCode.F6))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F6: toggle hard MesOK SwitchOK gate (vA15).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F6 (toggle MesOK SwitchOK gate)");
                        GameDebugMenuBridge.HotkeyToggleMesOkSwitch();
                        return;
                    }

                    DumpUiDelta();
                }

                if (Input.GetKeyDown(KeyCode.F7))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F7: toggle FieldPlayerStop overlay for FieldUnitPadRelease strategy (vA18).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F7 (toggle FieldPlayerStop overlay for FieldUnitPadRelease)");
                        GameDebugMenuBridge.HotkeyToggleUnitPadFieldStopOverlay();
                        return;
                    }

                    DumpUiWatchlist();
                }

                if (Input.GetKeyDown(KeyCode.F9))
                {
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    if (ctrl && alt)
                        GameDebugMenuBridge.HotkeyToggleEveHitHardBlock();
                    else
                        DumpUiAnchorComponents();
                }

if (Input.GetKeyDown(KeyCode.F10))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                    // Ctrl+Alt+Shift+F10: dump the current Shift+F1 HUD contents (compact, shareable text).
                    if (ctrlHeld && altHeld && shiftHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F10 (dump DevTools HUD text)");
                        DumpDevToolsHudText();
                        return;
                    }

                    // Ctrl+Alt+F10: toggle sequence trace (logs dds3SequenceList transitions to help map vanilla menu routing).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F10 (toggle sequence trace)");
                        GameDebugMenuBridge.HotkeyToggleSeqTrace();
                        return;
                    }

                    DumpReflectionSurface();
                }

                if (Input.GetKeyDown(KeyCode.F11))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                    // Ctrl+Alt+Shift+F11: dump the game-native debug menu tree/state (cmpTest)
                    // Use this while the DEBUG MENU is OPEN to map the built-in demon/item/skill editors.
                    if (ctrlHeld && altHeld && shiftHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F11 (dump native debug menu tree)");
                        GameDebugMenuBridge.HotkeyDumpNativeDebugMenuTree();
                        return;
                    }

                    // Ctrl+Alt+F11: dump camp/command-menu reflection surface (vanilla RE helper).
                    // Do this while the vanilla command menu is OPEN to discover the real member names for menu lists, cursor objects, etc.
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F11 (dump camp/command-menu reflection surface)");
                        GameDebugMenuBridge.HotkeyDumpCampReflectionSurface();
                        return;
                    }

                    DumpUiComponentInventory();
                }

                if (Input.GetKeyDown(KeyCode.F12))
                {
                    // Keep plain F12 as the existing controller-state dump.
                    // Use Ctrl+Alt+F12 for a lightweight camp selection summary dump.
                    // Use Ctrl+Alt+Shift+F12 for a targeted UnitWork surface dump of the highlighted selection.
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                    if (ctrlHeld && altHeld && shiftHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+Shift+F12 (dump camp selected-unit surface)");
                        GameDebugMenuBridge.HotkeyDumpCampSelectedUnitSurface();
                    }
                    else if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F12 (dump camp selection summary)");
                        GameDebugMenuBridge.HotkeyDumpCampSelectionSummary();
                    }
                    else
                    {
                        DumpUiControllerState();
                    }
                }

                if (Input.GetKeyDown(KeyCode.F3))
                {
                    bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                    // Ctrl+Alt+F3: toggle Mes OK/CANCEL remap while debug-menu capture is active (experimental).
                    if (ctrlHeld && altHeld)
                    {
                        MelonLogger.Msg("[Reimagined] Hotkey: Ctrl+Alt+F3 (toggle itfMesManager.OK remap while debug-menu capture is active)");
                        GameDebugMenuBridge.HotkeyToggleMesOkRemap();
                        return;
                    }

                    DumpTerminalSeamState();
                }

                // Pump any deferred debug-menu cleanup requested by menu callbacks.
                GameDebugMenuBridge.PumpDeferredClose();
                FieldInteractionSuppression.TryInstall();
	            GameDebugMenuBridge.PumpMenuPause();
                GameDebugMenuBridge.PumpInputBlock();
                GameDebugMenuBridge.PumpPadProbe();
	                GameDebugMenuBridge.PumpFieldWarpTrace();

                if (_overlayOpen)
                {
                    TickUguiOverlay();


                    if (_captureInputsWhenOpen)
                        ResetInputAxesBestEffort();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] OnUpdate exception: {ex}");
                LogJson("error", "onupdate_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
            }
        }

        private void ToggleDevtoolsMode()
        {
            bool wantOpen = !_overlayOpen;

            if (wantOpen)
            {
                _overlayOpen = true;

                // Attempt to create overlay immediately. If it fails, roll back and do not apply pause/cursor.
                if (!EnsureUguiOverlay())
                {
                    _overlayOpen = false;

                    string err = _uguiOverlayLastCreateError ?? "unknown error";
                    MelonLogger.Warning($"[Reimagined] DevTools overlay failed to open: {err}");
                    LogJson("warn", "devtools_open_failed", new Dictionary<string, object?>
                    {
                        ["error"] = err
                    });

                    OverlayLog("warn", $"overlay_open_failed: {err}");
                    Toast("Overlay failed to open (see log)", ToastKind.Error);

                    // Ensure we don't leave cursor/timescale in a weird state.
                    ClearPauseBestEffort();
                    ClearCursorForOverlayBestEffort();
                    return;
                }
            }
            else
            {
                _overlayOpen = false;
            }

            MelonLogger.Msg($"[Reimagined] DevTools overlay = {_overlayOpen}.");
            LogJson("devtools", "toggle", new Dictionary<string, object?>
            {
                ["open"] = _overlayOpen
            });

            OverlayLog("devtools", $"overlay_toggle open={_overlayOpen}");
            Toast(_overlayOpen ? "Overlay opened" : "Overlay closed", ToastKind.Info);

            _uguiOverlay?.SetOpen(_overlayOpen);

            if (_pauseWhenOpen)
            {
                if (_overlayOpen)
                    ApplyPauseBestEffort();
                else
                    ClearPauseBestEffort();
            }

            // HUD overlay is read-only and does not require cursor capture.
            // Only restore cursor state when closing (if we had captured it in an older build / other code path).
            if (!_overlayOpen)
                ClearCursorForOverlayBestEffort();
        }

        private void OpenGameDebugMenu()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (GameDebugMenuBridge.TryToggleReimaginedMenu(this, out string status))
                {
                    MelonLogger.Msg($"[Reimagined] Game debug menu: {status}");
                    LogJson("devmenu", "open", new Dictionary<string, object?> { ["status"] = status });
                }
                else
                {
                    MelonLogger.Warning($"[Reimagined] Game debug menu: {status}");
                    LogJson("warn", "devmenu_open_failed", new Dictionary<string, object?> { ["status"] = status });
                    // No UI toast here: if the menu system is broken we don't want to depend on the overlay.
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Reimagined] Game debug menu open failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Bridge into the game's existing debug menu (cmpTest) so Reimagined can present a menu that behaves
        /// like native UI (font, focus, input routing), instead of fighting a custom overlay.
        ///
        /// Pass A6: keep this intentionally minimal and low-risk:
        /// - Inject a single root entry "Reimagined" that opens a static submenu with guidance.
        /// - We do NOT try to create custom Il2Cpp delegates yet (action items remain hotkey-driven).
        /// - We log everything and fail closed.
        /// </summary>
        
        // (moved) GameDebugMenuBridge lives in DevTools/GameDebugMenuBridge.cs


                // --------------------------
        // Overlay (UGUI bootstrap)
        // --------------------------

        private void OverlayLog(string category, string message)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (_overlayLog.Count >= OverlayLogMax)
                {
                    int remove = _overlayLog.Count - OverlayLogMax + 1;
                    _overlayLog.RemoveRange(0, remove);
                }

                _overlayLog.Add(new OverlayLogLine(DateTime.Now, category, message));
            }
            catch
            {
                // Never let the overlay crash the mod.
            }
        }

        private void Toast(string message, ToastKind kind)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var ttl = kind == ToastKind.Error ? 6.0 : (kind == ToastKind.Warn ? 5.0 : 3.0);
                _toasts.Add(new OverlayToast(DateTime.Now.AddSeconds(ttl), kind, message));
                if (_toasts.Count > 8)
                    _toasts.RemoveAt(0);
            }
            catch
            {
                // ignore
            }
        }

        private static void SafeCall(Action fn)
        {
            try { fn(); } catch { }
        }


        // --------------------------
        // Unity IL2CPP compat helpers
        // --------------------------
        //
        // Important: Many Unity API overloads that take System.Type do NOT exist in Il2Cpp builds.
        // If we bind to those at compile-time (e.g. go.AddComponent(typeof(Foo))), the mod will
        // compile but throw MissingMethodException at runtime. These helpers resolve and invoke
        // the Il2CppSystem.Type overloads via reflection so the overlay can bootstrap reliably.
        //
        private static MethodInfo? s_goAddComponent_Il2CppType;
        private static MethodInfo? s_goAddComponent_SystemType;
        private static MethodInfo? s_goGetComponent_Il2CppType;
        private static MethodInfo? s_goGetComponent_SystemType;
        private static MethodInfo? s_resGetBuiltin_Il2CppType;
        private static MethodInfo? s_resGetBuiltin_SystemType;
        private static MethodInfo? s_objFindOfType_Il2Cpp_1;
        private static MethodInfo? s_objFindOfType_Il2Cpp_2;
        private static MethodInfo? s_objFindOfType_System_1;
        private static MethodInfo? s_objFindOfType_System_2;

        private static PropertyInfo? s_il2cppObjBase_PointerProp;
        private static FieldInfo? s_il2cppObjBase_PointerField;

        private static IntPtr TryGetIl2CppPointer(object obj, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (obj is Il2CppObjectBase ib)
                {
                    if (s_il2cppObjBase_PointerProp == null && s_il2cppObjBase_PointerField == null)
                    {
                        var t = typeof(Il2CppObjectBase);
                        s_il2cppObjBase_PointerProp =
                            t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? t.GetProperty("NativePtr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (s_il2cppObjBase_PointerProp == null)
                        {
                            s_il2cppObjBase_PointerField =
                                t.GetField("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                ?? t.GetField("NativePtr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                    }

                    if (s_il2cppObjBase_PointerProp != null && s_il2cppObjBase_PointerProp.PropertyType == typeof(IntPtr))
                        return (IntPtr)(s_il2cppObjBase_PointerProp.GetValue(ib) ?? IntPtr.Zero);

                    if (s_il2cppObjBase_PointerField != null && s_il2cppObjBase_PointerField.FieldType == typeof(IntPtr))
                        return (IntPtr)(s_il2cppObjBase_PointerField.GetValue(ib) ?? IntPtr.Zero);
                }

                // Fallback: some wrappers expose Pointer on their own type.
                var rt = obj.GetType();
                var p = rt.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(IntPtr))
                    return (IntPtr)(p.GetValue(obj) ?? IntPtr.Zero);
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<TryGetIl2CppPointer failed: {ex.GetType().Name}: {ex.Message}>");
            }

            return IntPtr.Zero;
        }

        private static TUnity? TryWrapIl2CppObject<TUnity>(object? obj, TextWriter? diag = null) where TUnity : class
        {
            if (obj == null)
                return null;

            if (obj is TUnity direct)
                return direct;

            var ptr = TryGetIl2CppPointer(obj, diag);
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var t = typeof(TUnity);

                // Most Il2CppInterop-generated wrapper types expose a (IntPtr) ctor.
                var ctor1 = t.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(IntPtr) },
                    modifiers: null);

                if (ctor1 != null)
                    return ctor1.Invoke(new object?[] { ptr }) as TUnity;

                // Some wrappers use (IntPtr, bool) where bool controls ownership.
                var ctor2 = t.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(IntPtr), typeof(bool) },
                    modifiers: null);

                if (ctor2 != null)
                    return ctor2.Invoke(new object?[] { ptr, false }) as TUnity;
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<TryWrapIl2CppObject failed for {typeof(TUnity).FullName}: {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private static Component? AddComponentCompat(GameObject go, Type componentType, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Prefer Il2CppSystem.Type overload.
                if (s_goAddComponent_Il2CppType == null)
                {
                    s_goAddComponent_Il2CppType = typeof(GameObject)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "AddComponent") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType.FullName == "Il2CppSystem.Type";
                            }
                            catch { return false; }
                        });
                }

                if (s_goAddComponent_Il2CppType != null)
                {
                    var ilType = TryMakeIl2CppSystemType(componentType, diag);
                    if (ilType != null)
                    {
                        var res = s_goAddComponent_Il2CppType.Invoke(go, new object?[] { ilType });
                        return TryWrapIl2CppObject<Component>(res, diag);
                    }
                }

                // Fallback: System.Type overload (useful if running in a Mono/Editor-like environment).
                if (s_goAddComponent_SystemType == null)
                {
                    s_goAddComponent_SystemType = typeof(GameObject)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "AddComponent") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                            }
                            catch { return false; }
                        });
                }

                if (s_goAddComponent_SystemType != null)
                {
                    var res = s_goAddComponent_SystemType.Invoke(go, new object?[] { componentType });
                    return TryWrapIl2CppObject<Component>(res, diag);
                }
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<AddComponentCompat failed for {componentType.FullName}: {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private static Component? GetComponentCompat(GameObject go, Type componentType, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Prefer Il2CppSystem.Type overload.
                if (s_goGetComponent_Il2CppType == null)
                {
                    s_goGetComponent_Il2CppType = typeof(GameObject)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "GetComponent") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType.FullName == "Il2CppSystem.Type";
                            }
                            catch { return false; }
                        });
                }

                if (s_goGetComponent_Il2CppType != null)
                {
                    var ilType = TryMakeIl2CppSystemType(componentType, diag);
                    if (ilType != null)
                    {
                        var res = s_goGetComponent_Il2CppType.Invoke(go, new object?[] { ilType });
                        return TryWrapIl2CppObject<Component>(res, diag);
                    }
                }

                // Fallback: System.Type overload.
                if (s_goGetComponent_SystemType == null)
                {
                    s_goGetComponent_SystemType = typeof(GameObject)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "GetComponent") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                            }
                            catch { return false; }
                        });
                }

                if (s_goGetComponent_SystemType != null)
                {
                    var res = s_goGetComponent_SystemType.Invoke(go, new object?[] { componentType });
                    return TryWrapIl2CppObject<Component>(res, diag);
                }
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<GetComponentCompat failed for {componentType.FullName}: {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private static T? GetComponentCompat<T>(GameObject go, TextWriter? diag = null) where T : Component
        {
            var res = GetComponentCompat(go, typeof(T), diag);
            return TryWrapIl2CppObject<T>(res, diag);
        }

        private static UnityEngine.Object? GetBuiltinResourceCompat(Type resourceType, string name, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Prefer Il2CppSystem.Type overload.
                if (s_resGetBuiltin_Il2CppType == null)
                {
                    s_resGetBuiltin_Il2CppType = typeof(Resources)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "GetBuiltinResource") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType.FullName == "Il2CppSystem.Type" && ps[1].ParameterType == typeof(string);
                            }
                            catch { return false; }
                        });
                }

                if (s_resGetBuiltin_Il2CppType != null)
                {
                    var ilType = TryMakeIl2CppSystemType(resourceType, diag);
                    if (ilType != null)
                        return TryWrapIl2CppObject<UnityEngine.Object>(s_resGetBuiltin_Il2CppType.Invoke(null, new object?[] { ilType, name }), diag);
                }

                // Fallback: System.Type overload.
                if (s_resGetBuiltin_SystemType == null)
                {
                    s_resGetBuiltin_SystemType = typeof(Resources)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "GetBuiltinResource") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType == typeof(Type) && ps[1].ParameterType == typeof(string);
                            }
                            catch { return false; }
                        });
                }

                if (s_resGetBuiltin_SystemType != null)
                    return TryWrapIl2CppObject<UnityEngine.Object>(s_resGetBuiltin_SystemType.Invoke(null, new object?[] { resourceType, name }), diag);
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<GetBuiltinResourceCompat failed for {resourceType.FullName} '{name}': {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private static Font? TryCreateDynamicFontFromOSFont(string fontName, int size, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var mi = typeof(Font).GetMethod("CreateDynamicFontFromOSFont", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null);
                if (mi == null)
                    return null;

                return mi.Invoke(null, new object?[] { fontName, size }) as Font;
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<TryCreateDynamicFontFromOSFont failed '{fontName}' {size}: {ex.GetType().Name}: {ex.Message}>");
                return null;
            }
        }


        private static UnityEngine.Object? FindObjectOfTypeCompat(Type type, bool includeInactive = true, TextWriter? diag = null)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Prefer Il2CppSystem.Type overloads if present.
                if (s_objFindOfType_Il2Cpp_1 == null || s_objFindOfType_Il2Cpp_2 == null)
                {
                    foreach (var m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name != "FindObjectOfType") continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType.FullName == "Il2CppSystem.Type")
                            s_objFindOfType_Il2Cpp_1 = m;
                        else if (ps.Length == 2 && ps[0].ParameterType.FullName == "Il2CppSystem.Type" && ps[1].ParameterType == typeof(bool))
                            s_objFindOfType_Il2Cpp_2 = m;
                    }
                }

                var ilType = TryMakeIl2CppSystemType(type, diag);

                if (ilType != null)
                {
                    if (s_objFindOfType_Il2Cpp_2 != null)
                        return TryWrapIl2CppObject<UnityEngine.Object>(s_objFindOfType_Il2Cpp_2.Invoke(null, new object?[] { ilType, includeInactive }), diag);
                    if (s_objFindOfType_Il2Cpp_1 != null)
                        return TryWrapIl2CppObject<UnityEngine.Object>(s_objFindOfType_Il2Cpp_1.Invoke(null, new object?[] { ilType }), diag);
                }

                // Fallback: System.Type overloads.
                if (s_objFindOfType_System_1 == null || s_objFindOfType_System_2 == null)
                {
                    s_objFindOfType_System_1 = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type) });
                    s_objFindOfType_System_2 = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type), typeof(bool) });
                }

                if (s_objFindOfType_System_2 != null)
                    return TryWrapIl2CppObject<UnityEngine.Object>(s_objFindOfType_System_2.Invoke(null, new object?[] { type, includeInactive }), diag);
                if (s_objFindOfType_System_1 != null)
                    return TryWrapIl2CppObject<UnityEngine.Object>(s_objFindOfType_System_1.Invoke(null, new object?[] { type }), diag);
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"<FindObjectOfTypeCompat failed for {type.FullName}: {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private void ApplyCursorForOverlayBestEffort()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (_cursorCaptured) return;
                _prevCursorVisible = Cursor.visible;
                _prevCursorLockState = Cursor.lockState;

                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                _cursorCaptured = true;
            }
            catch
            {
                // ignore
            }
        }

        private void ClearCursorForOverlayBestEffort()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (!_cursorCaptured) return;
                Cursor.visible = _prevCursorVisible;
                Cursor.lockState = _prevCursorLockState;
                _cursorCaptured = false;
            }
            catch
            {
                // ignore
            }
        }

        
        private static void ResetInputAxesBestEffort()
        {
            // Some Unity builds (or trimmed bindings) do not expose Input.ResetInputAxes().
            // Use reflection so we can compile against reduced Unity stubs safely.
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var m = typeof(Input).GetMethod("ResetInputAxes", BindingFlags.Public | BindingFlags.Static);
                if (m != null)
                    m.Invoke(null, null);
            }
            catch
            {
                // ignore
            }
        }

        private bool EnsureUguiOverlay()
        {
            if (_uguiOverlay != null)
                return true;

            var now = DateTime.Now;
            if (now < _uguiOverlayRetryAfter)
                return false;

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                _uguiOverlay = new DevToolsOverlayUGUI(this);
                _uguiOverlay.SetOpen(_overlayOpen);

                _uguiOverlayCreateFailures = 0;
                _uguiOverlayLastCreateError = null;
                return true;
            }
            catch (Exception ex)
            {
                _uguiOverlayCreateFailures++;
                _uguiOverlayLastCreateError = $"{ex.GetType().Name}: {ex.Message}";

                // Backoff to avoid log spam if creation is failing every frame.
                int backoffSec = _uguiOverlayCreateFailures >= 3 ? 30 : 2;
                _uguiOverlayRetryAfter = now.AddSeconds(backoffSec);

                MelonLogger.Warning($"[Reimagined] Failed to create DevTools UGUI overlay (attempt {_uguiOverlayCreateFailures}): {_uguiOverlayLastCreateError}");
                LogJson("warn", "ugui_overlay_create_failed", new Dictionary<string, object?>
                {
                    ["attempt"] = _uguiOverlayCreateFailures,
                    ["error"] = _uguiOverlayLastCreateError
                });

                return false;
            }

        }
        private void TickUguiOverlay()
        {
            if (!_overlayOpen)
                return;

            if (!EnsureUguiOverlay())
                return;

            _uguiOverlay?.Tick();
        }

        // --------------------------
        // Overlay (UGUI bootstrap)
        // --------------------------
        
        // --------------------------
        // Overlay (UGUI HUD)
        // --------------------------
        private sealed class DevToolsOverlayUGUI
        {
            // This overlay was intentionally redesigned as a small, read-only HUD:
            // - No pause / timeScale changes by default.
            // - No input capture by default.
            // - No EventSystem / GraphicRaycaster (we're not clicking anything).
            // This keeps it usable while navigating menus, and keeps the code surface small.

            private readonly ReimaginedMod _m;

            private GameObject _root;
            private GameObject _panel;

            private Text _titleText;
            private Text _contentText;
            private Text _toastText;

            private DateTime _nextRefresh = DateTime.MinValue;
	            private string _lastText = "";

            // Legacy helper (used by button helpers below; we keep it so the file compiles even if buttons are unused).
            private static bool s_warnedUnityActionNotManagedDelegate;

            public DevToolsOverlayUGUI(ReimaginedMod m)
            {
                _m = m;

                try
                {
                    GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                    _root = new GameObject("ReimaginedDevToolsHud");
                    UnityEngine.Object.DontDestroyOnLoad(_root);

                    // Avoid Unity generic AddComponent<T>()/GetComponent<T>() here: some shipped reference surfaces omit generics.
                    AddComponentCompat(_root, typeof(Canvas));
                    var canvas = GetComponentCompat<Canvas>(_root);
                    if (canvas == null) throw new Exception("UGUI bootstrap failed: Canvas missing after AddComponent.");
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 9999;

                    AddComponentCompat(_root, typeof(CanvasScaler));
                    var scaler = GetComponentCompat<CanvasScaler>(_root);
                    if (scaler == null) throw new Exception("UGUI bootstrap failed: CanvasScaler missing after AddComponent.");
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);

                    // NOTE: Intentionally no GraphicRaycaster / EventSystem.
                    // This HUD is read-only; removing the input system avoids messing with the game's UI.

	                    // Slightly taller/wider HUD panel so the recent log and skill block fit comfortably.
	                    _panel = CreatePanel(_root.transform, new Vector2(16, -16), new Vector2(700, 470));

	                    _titleText = CreateText(_panel.transform, "Title", new Vector2(10, -8), new Vector2(680, 24), 18, FontStyle.Bold);
                    _titleText.text = "SMT3HD_Reimagined · DevTools HUD";

	                    _contentText = CreateText(_panel.transform, "Content", new Vector2(10, -36), new Vector2(680, 395), 13, FontStyle.Normal);
                    _contentText.alignment = TextAnchor.UpperLeft;
                    _contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    _contentText.verticalOverflow = VerticalWrapMode.Overflow;

	                    _toastText = CreateText(_panel.transform, "Toast", new Vector2(10, -438), new Vector2(680, 22), 12, FontStyle.Italic);
                    _toastText.alignment = TextAnchor.MiddleLeft;

                    SetOpen(false);
                }
                catch
                {
                    try
                    {
                        if (_root != null)
                        {
                            _root.SetActive(false);
                            UnityEngine.Object.Destroy(_root);
                        }
                    }
                    catch { }

                    throw;
                }
            }

            public void SetOpen(bool open)
            {
                if (_root != null)
                    _root.SetActive(open);
            }

            public void Tick()
            {
                if (_root == null || !_root.activeSelf)
                    return;

                var now = DateTime.Now;
                if (now < _nextRefresh)
                    return;

                _nextRefresh = now.AddMilliseconds(200);

	                try
	                {
	                    _lastText = BuildHudText();
	                    _contentText.text = _lastText;
	                }
	                catch (Exception ex)
	                {
	                    _lastText = $"<hud error: {ex.GetType().Name}: {ex.Message}>";
	                    _contentText.text = _lastText;
	                }

                try
                {
                    _toastText.text = GetToastText();
                }
                catch
                {
                    // ignore
                }
            }

            public string GetCurrentText()
            {
                return _lastText;
            }

            private string BuildHudText()
            {
                return _m.BuildDevToolsHudText(dumpMode: false);
            }

private string GetToastText()
            {
                try
                {
                    var now = DateTime.Now;
                    for (int i = _m._toasts.Count - 1; i >= 0; i--)
                    {
                        var t = _m._toasts[i];
                        if (t.Until > now)
                        {
                            string tag;
                            if (t.Kind == ToastKind.Ok) tag = "OK";
                            else if (t.Kind == ToastKind.Warn) tag = "WARN";
                            else if (t.Kind == ToastKind.Error) tag = "ERR";
                            else tag = "INFO";
                            return $"[{tag}] {S(t.Message)}";
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                return "";
            }

            private static string S(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";

                // Keep HUD readable: avoid newlines and control chars.
                return s.Replace("\r", "").Replace("\n", "\\n");
            }

            private static GameObject CreatePanel(Transform parent, Vector2 topLeft, Vector2 size)
            {
                var go = new GameObject("Panel");
                go.transform.SetParent(parent, worldPositionStays: false);

                // Avoid Unity generic AddComponent<T>()/GetComponent<T>() here: some shipped reference surfaces omit generics.
                // Use AddComponentCompat + GetComponentCompat to stay compile-safe across SMT3HD's IL2CPP bindings.
                AddComponentCompat(go, typeof(RectTransform));
                var rt = GetComponentCompat<RectTransform>(go);
                if (rt == null) throw new Exception("UGUI element bootstrap failed: RectTransform missing after AddComponent.");
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = topLeft;
                rt.sizeDelta = size;

                AddComponentCompat(go, typeof(Image));
                var img = GetComponentCompat<Image>(go);
                if (img == null) throw new Exception("UGUI element bootstrap failed: Image missing after AddComponent.");
                img.color = new Color(0, 0, 0, 0.78f);

                return go;
            }

            private static Text CreateText(Transform parent, string name, Vector2 topLeft, Vector2 size, int fontSize, FontStyle style)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, worldPositionStays: false);

                AddComponentCompat(go, typeof(RectTransform));
                var rt = GetComponentCompat<RectTransform>(go);
                if (rt == null) throw new Exception("UGUI element bootstrap failed: RectTransform missing after AddComponent.");
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = topLeft;
                rt.sizeDelta = size;

                AddComponentCompat(go, typeof(Text));
                var txt = GetComponentCompat<Text>(go);
                if (txt == null) throw new Exception("UGUI element bootstrap failed: Text missing after AddComponent.");
                txt.font = TryCreateDynamicFontFromOSFont("Arial", fontSize) ?? TryCreateDynamicFontFromOSFont("Segoe UI", fontSize) ?? TryWrapIl2CppObject<Font>(GetBuiltinResourceCompat(typeof(Font), "Arial.ttf"), null);
                txt.fontSize = fontSize;
                txt.fontStyle = style;
                txt.color = Color.white;
                txt.supportRichText = false;
                txt.text = "";
                txt.alignment = TextAnchor.UpperLeft;

                return txt;
            }

                        private static void TryBindUnityAction(Button btn, Action click, string nameForLog)
            {
                // IMPORTANT: In SMT3HD IL2CPP, UnityAction may resolve to an Il2Cpp delegate wrapper (not a managed delegate),
                // which makes Delegate.CreateDelegate explode ("Type must derive from Delegate"). When that happens we disable
                // clickable overlay buttons and rely on hotkeys instead.
                if (!typeof(UnityAction).IsSubclassOf(typeof(Delegate)))
                {
                    if (!s_warnedUnityActionNotManagedDelegate)
                    {
                        s_warnedUnityActionNotManagedDelegate = true;
                        MelonLogger.Warning("[Reimagined] DevTools UI: UnityAction is not a managed delegate in this runtime; overlay buttons are disabled (use hotkeys).");
                    }
                    return;
                }

                try
                {
                    // NOTE: In some SMT3HD UnityDependencies bindings, `new UnityAction(...)` can throw at runtime
                    // (MissingMethodException on UnityAction..ctor). `Delegate.CreateDelegate` is more resilient.
                    var ua = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), click.Target, click.Method);
                    btn.onClick.AddListener(ua);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Reimagined] DevTools UI: failed to wire onClick for '{nameForLog}': {ex.GetType().Name}: {ex.Message}");
                }
            }

private static Button CreateButtonWithLabel(Transform parent, string name, string label, Vector2 topLeft, Vector2 size, Action onClick, out Text labelText)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, worldPositionStays: false);

                AddComponentCompat(go, typeof(RectTransform));
                var rt = GetComponentCompat<RectTransform>(go);
                if (rt == null) throw new Exception("UGUI element bootstrap failed: RectTransform missing after AddComponent.");
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = topLeft;
                rt.sizeDelta = size;

                AddComponentCompat(go, typeof(Image));
                var img = GetComponentCompat<Image>(go);
                if (img == null) throw new Exception("UGUI element bootstrap failed: Image missing after AddComponent.");
                img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

                AddComponentCompat(go, typeof(Button));
                var btn = GetComponentCompat<Button>(go);
                if (btn == null) throw new Exception("UGUI element bootstrap failed: Button missing after AddComponent.");
                var click = (Action)(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Reimagined] DevTools UI action threw: {ex.GetType().Name}: {ex.Message}");
                    }
                });

                TryBindUnityAction(btn, click, name);

                labelText = CreateText(go.transform, "Text", new Vector2(0, 0), size, 14, FontStyle.Normal);
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.text = label;

                return btn;
            }

            private static Button CreateButton(Transform parent, string name, string label, Vector2 topLeft, Vector2 size, Action onClick)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, worldPositionStays: false);

                AddComponentCompat(go, typeof(RectTransform));
                var rt = GetComponentCompat<RectTransform>(go);
                if (rt == null) throw new Exception("UGUI element bootstrap failed: RectTransform missing after AddComponent.");
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = topLeft;
                rt.sizeDelta = size;

                AddComponentCompat(go, typeof(Image));
                var img = GetComponentCompat<Image>(go);
                if (img == null) throw new Exception("UGUI element bootstrap failed: Image missing after AddComponent.");
                img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

                AddComponentCompat(go, typeof(Button));
                var btn = GetComponentCompat<Button>(go);
                if (btn == null) throw new Exception("UGUI element bootstrap failed: Button missing after AddComponent.");
                var click = (Action)(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Reimagined] DevTools UI action failed: {ex.GetType().Name}: {ex.Message}");
                    }
                });

                TryBindUnityAction(btn, click, name);

                var t = CreateText(go.transform, "Text", new Vector2(8, -4), new Vector2(size.x - 16, size.y - 8), 13, FontStyle.Normal);
                t.alignment = TextAnchor.MiddleLeft;
                t.text = label;

                return btn;
            }
        }

        private void RefreshUiWatchCacheBestEffort()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, TextWriter.Null, 128);

                int maxNodes = 9000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                _uiWatchCache = snap;
                _uiWatchCacheSig = BuildRootSignature(scene, roots);
                _uiWatchCacheTs = DateTime.Now;
            }
            catch (Exception ex)
            {
                OverlayLog("warn", $"watch_cache_refresh_failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void DumpStatusSnapshot()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"status_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                var scene = SceneManager.GetActiveScene();
                var data = new Dictionary<string, object?>
                {
                    ["schema_version"] = SchemaVersion,
                    ["ts"] = DateTime.Now.ToString("O"),
                    ["unity"] = Application.unityVersion,
                    ["app_version"] = Application.version,
                    ["scene"] = scene.name,
                    ["scene_buildIndex"] = scene.buildIndex,
                    ["frame"] = GetFrameCountSafe(),
                    ["timeScale"] = Time.timeScale,
                    ["overlay_open"] = _overlayOpen,
                    ["pause_on_open"] = _pauseWhenOpen,
                    ["pause_applied"] = _pauseApplied,
                    ["prev_timescale"] = _prevTimeScale,
                    ["ui_snapshot_sig"] = _lastUiSnapshotSig,
                    ["ui_snapshot_ts"] = _lastUiSnapshotSig != null ? _lastUiSnapshotTs.ToString("O") : null,
                    ["watch_cache_sig"] = _uiWatchCacheSig,
                    ["watch_cache_ts"] = _uiWatchCache != null ? _uiWatchCacheTs.ToString("O") : null,
                };

                File.WriteAllText(path, SimpleJson(data), new UTF8Encoding(false));

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] Status snapshot written: {path}");
                LogJson("devtools", "status_snapshot_written", new Dictionary<string, object?> { ["path"] = path });
                OverlayLog("devtools", $"status_snapshot_written: {Path.GetFileName(path)}");
                Toast("Status snapshot written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpStatusSnapshot exception: {ex}");
                LogJson("error", "status_snapshot_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                Toast("Status snapshot failed", ToastKind.Error);
            }
        }

        private string BuildDevToolsHudText(bool dumpMode)
        {
            // Compact mode: readable in-game. Dump mode: richer, for sharing + offline analysis.
            var sb = new StringBuilder(dumpMode ? 4096 : 1400);

            string San(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";
                return s.Replace("\r", "").Replace("\n", "\\n");
            }

            if (dumpMode)
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                sb.AppendLine($"Scene: {scene.name} (buildIndex={scene.buildIndex})");
            }
            catch
            {
                sb.AppendLine("Scene: <unknown>");
            }

            // NOTE: Some Il2Cpp/Unity reference assemblies used by MelonLoader stubs omit Time.frameCount.
            // We avoid referencing it at compile-time and instead display unscaled time (stable and always available).
            sb.AppendLine($"UnscaledTime: {Time.unscaledTime:0.###}   TimeScale: {UnityEngine.Time.timeScale:0.###}");
            sb.AppendLine($"Overlay settings: pauseOnOpen={_pauseWhenOpen}  captureInputs={_captureInputsWhenOpen}  terminalInvokes={_allowTerminalInvokes}");
            sb.AppendLine($"Last dump: {(_lastDumpPath ?? "<none>")}");

            if (dumpMode)
            {
                sb.AppendLine($"Overlay open: {_overlayOpen}   pauseApplied={_pauseApplied}");
            }

            sb.AppendLine();

            sb.AppendLine("[Native Debug Menu · Skill]");
            try
            {
                if (GameDebugMenuBridge.TryGetNativeDebugMenuSkillEditorState(out var nm))
                {
                    string unitTag = nm.HasUnit ? San(nm.Unit.NameTag) : "<none>";
                    sb.AppendLine($"{nm.Phase} / {nm.Subphase}   unit={unitTag}");

                    if (nm.HasUnit && dumpMode)
                    {
                        sb.AppendLine($"unitworkIndex={nm.Unit.UnitworkIndex}  unitId={nm.Unit.UnitId}  lvl={nm.Unit.Level}  hp={nm.Unit.HP}/{nm.Unit.MaxHP}  mp={nm.Unit.MP}/{nm.Unit.MaxMP}");
                        if (nm.Unit.UniqueId != 0)
                            sb.AppendLine($"uniqueId={nm.Unit.UniqueId}  ptr=0x{nm.Unit.Ptr:X}");
                    }

                    if (nm.SlotValid)
                        sb.AppendLine($"slot={nm.Slot}   slotCurrent={nm.SlotCurrentSkillId} \"{San(nm.SlotCurrentSkillName)}\"");
                    else
                        sb.AppendLine($"slot={nm.Slot} (invalid)");

                    if (nm.ObservedSkillId >= 0)
                        sb.AppendLine($"observed={nm.ObservedSkillId} \"{San(nm.ObservedSkillName)}\"");

                    if (nm.Subphase == GameDebugMenuBridge.NativeDebugMenuSkillEditorSubphase.ReplacementPick && nm.CandidateSkillId >= 0)
                        sb.AppendLine($"candidate={nm.CandidateSkillId} \"{San(nm.CandidateSkillName)}\"");

                    if (!string.IsNullOrEmpty(nm.Note))
                        sb.AppendLine($"note: {San(nm.Note)}");

                    if (GameDebugMenuBridge.TryGetLastSkillEditSummary(out string lastEditSummary))
                        sb.AppendLine($"lastEdit: {San(lastEditSummary)}");

                    GameDebugMenuBridge.GetSkillEditHistoryCounts(out int undoN, out int redoN);
                    sb.AppendLine($"editHistory: undo={undoN} redo={redoN}");

                    if (GameDebugMenuBridge.TryGetSkillFavoriteSummary(out string favSummary))
                        sb.AppendLine($"favorite: {San(favSummary)}");

                    if (GameDebugMenuBridge.TryGetSkillFavoritesSourceSummary(out string favSrc))
                        sb.AppendLine($"favoritesSrc: {San(favSrc)}");

                    if (dumpMode && nm.HasUnit)
                    {
                        sb.AppendLine();
                        sb.AppendLine("[Native Debug Menu · Skill · Unit Skills]");
                        if (GameDebugMenuBridge.TryGetUnitSkillSnapshot(nm.Unit.UnitworkIndex, out var snap))
                        {
                            sb.AppendLine($"skillcnt={snap.SkillCnt}  arrLen={snap.SkillArrLen}  slots={snap.Slots.Length}");
                            if (!string.IsNullOrEmpty(snap.Note))
                                sb.AppendLine($"note: {San(snap.Note)}");

                            for (int i = 0; i < snap.Slots.Length; i++)
                            {
                                var s = snap.Slots[i];
                                sb.AppendLine($"  [{s.Slot}] id={s.SkillId} \"{San(s.SkillName)}\"");
                            }
                        }
                        else
                        {
                            sb.AppendLine("unavailable (unitwork probe failed)");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("unavailable (open native debug menu → SKILL)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"error: {ex.GetType().Name}");
            }


            sb.AppendLine();
            sb.AppendLine("[Field · Map/Warp]");
            if (GameDebugMenuBridge.TryGetFieldMapHudLines(dumpMode, out var fieldLines))
            {
                for (int i = 0; i < fieldLines.Length; i++)
                    sb.AppendLine(fieldLines[i]);
            }
            else
            {
                sb.AppendLine("unavailable (not in field / not loaded)");
            }

            if (GameDebugMenuBridge.TryGetWarpFavoriteSummary(out string warpFavSummary))
                sb.AppendLine($"warpFavorite: {San(warpFavSummary)}");

            if (GameDebugMenuBridge.TryGetWarpFavoritesSourceSummary(out string warpFavSrc))
                sb.AppendLine($"warpFavoritesSrc: {San(warpFavSrc)}");

            if (GameDebugMenuBridge.TryGetWarpCatalogRouteFavoritesSummary(out string warpRouteFavs))
                sb.AppendLine($"warpRouteFavorites: {San(warpRouteFavs)}");

            if (GameDebugMenuBridge.TryGetWarpCatalogRouteFavoriteBrowseSummary(out string warpRouteFavBrowse))
                sb.AppendLine($"warpRouteFavoriteBrowse: {San(warpRouteFavBrowse)}");

            if (GameDebugMenuBridge.TryGetWarpArmSummary(out string warpArm))
                sb.AppendLine($"warpArm: {San(warpArm)}");
            sb.AppendLine();
            sb.AppendLine("[Hotkeys]");
            sb.AppendLine("Shift+F1: toggle HUD");
            sb.AppendLine("Ctrl+Alt+Shift+K: apply candidate   Ctrl+Alt+Shift+U: undo   Ctrl+Alt+Shift+Y: redo");
            sb.AppendLine("Ctrl+Alt+Shift+H/L: favorite prev/next   Ctrl+Alt+Shift+G: apply favorite   Ctrl+Alt+Shift+R: reload favorites file");
            sb.AppendLine("Ctrl+Alt+Shift+[/]: warp fav prev/next (or Left/Right Arrow)   Ctrl+Alt+Shift+F8: warp confirm   Ctrl+Alt+Shift+F7: reload warp favorites   Ctrl+Alt+Shift+F9: dump warp table");
            sb.AppendLine("Ctrl+Alt+Shift+P: record next route   Ctrl+Alt+Shift+J: dump warp catalog summary   Ctrl+Alt+Shift+I: dump selected route   Ctrl+Alt+Shift+V: dump favorite snippet/archive");
            sb.AppendLine("Ctrl+Alt+Shift+A: append selected door route to warp favorites   Ctrl+Alt+Shift+C: save selected route to route favorites   Ctrl+Alt+Shift+;/' : route-fav prev/next   Ctrl+Alt+Shift+\\: dump selected route-fav");
            sb.AppendLine("Ctrl+Alt+Shift+/: browse mode toggle   Ctrl+Alt+Shift+,/.: browse prev/next catalog route");
            sb.AppendLine("F2: full scene probe   F3: terminal seam   F4: capabilities");
            sb.AppendLine("F5: UI signature   F6: UI delta   F7: UI watchlist");
            sb.AppendLine("Ctrl+Alt+Shift+F11: native debug menu tree dump");
            sb.AppendLine("Ctrl+Alt+Shift+F10: dump DevTools HUD text");
            sb.AppendLine("Ctrl+Alt+F12: camp selection summary   Ctrl+Alt+Shift+F12: dump selected unitwork");

            sb.AppendLine();
            sb.AppendLine("[Recent]");
            int keep = dumpMode ? 20 : 6;
            int start = Math.Max(0, _overlayLog.Count - keep);
            for (int i = start; i < _overlayLog.Count; i++)
            {
                var ln = _overlayLog[i];
                sb.AppendLine($"{ln.Ts:HH:mm:ss} [{ln.Category}] {San(ln.Message)}");
            }

            return sb.ToString();
        }

void DumpDevToolsHudText()
        {
            try
            {
                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"devtools_hud_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                string text = BuildDevToolsHudText(dumpMode: true);

                if (string.IsNullOrWhiteSpace(text))
                    text = "<HUD dump unavailable>";

                File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                _lastDumpPath = path;
                Toast($"HUD dump -> {Path.GetFileName(path)}", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpDevToolsHudText exception: {ex}");
            }
        }

        private void ApplyPauseBestEffort()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (_pauseApplied) return;

                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pauseApplied = true;

                MelonLogger.Msg("[Reimagined] Pause applied (Time.timeScale = 0). Note: some systems may use unscaled/custom time.");
                LogJson("devtools", "pause_applied", new Dictionary<string, object?>
                {
                    ["prev_timescale"] = _prevTimeScale
                });

                // Optional: try to pause audio via reflection (do not hard-reference type).
                // If it fails, it fails quietly; we’ll refine later once we find the game’s pause manager.
                TrySetAudioListenerPause(true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Reimagined] ApplyPauseBestEffort failed: {ex.GetType().Name}: {ex.Message}");
                LogJson("warn", "pause_apply_failed", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
            }
        }

        private void ClearPauseBestEffort()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (!_pauseApplied) return;

                Time.timeScale = _prevTimeScale;
                _pauseApplied = false;

                MelonLogger.Msg($"[Reimagined] Pause cleared (Time.timeScale restored to {_prevTimeScale}).");
                LogJson("devtools", "pause_cleared", new Dictionary<string, object?>
                {
                    ["restored_timescale"] = _prevTimeScale
                });

                TrySetAudioListenerPause(false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Reimagined] ClearPauseBestEffort failed: {ex.GetType().Name}: {ex.Message}");
                LogJson("warn", "pause_clear_failed", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
            }
        }

        private static void TrySetAudioListenerPause(bool paused)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // UnityEngine.AudioListener.pause is a static bool in many Unity versions.
                // We avoid referencing AudioListener directly since some builds strip it from the managed surface.
                var audioListenerType = FindTypeInLoadedAssemblies("UnityEngine.AudioListener");
                if (audioListenerType == null) return;

                var prop = audioListenerType.GetProperty("pause", BindingFlags.Public | BindingFlags.Static);
                if (prop == null || prop.PropertyType != typeof(bool)) return;

                prop.SetValue(null, paused, null);
                MelonLogger.Msg($"[Reimagined] AudioListener.pause = {paused} (reflection).");
            }
            catch
            {
                // Silent by design. Audio pausing is not a P0 requirement.
            }
        }

        // --------------------------
        // Dumps (P0 core value)
        // --------------------------

        private void DumpCapabilities()
        {
            Directory.CreateDirectory(DumpsDir);
            var path = Path.Combine(DumpsDir, $"capabilities_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

            w.WriteLine("SMT3HD_Reimagined Capabilities");
            w.WriteLine($"ts={DateTime.Now:O}");
            w.WriteLine($"unity={Application.unityVersion}");
            w.WriteLine($"app_version={Application.version}");
            w.WriteLine($"timescale={Time.timeScale}");
            w.WriteLine();

            w.WriteLine("Loaded Assemblies:");
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
            {
                var an = a.GetName();
                w.WriteLine($"- {an.Name}  v{an.Version}");
            }

            w.WriteLine();
            w.WriteLine("Key Types (presence check):");
            WriteTypePresence(w, "UnityEngine.SceneManagement.SceneManager");
            WriteTypePresence(w, "UnityEngine.Canvas");
            WriteTypePresence(w, "UnityEngine.EventSystems.EventSystem");
            WriteTypePresence(w, "UnityEngine.UI.Graphic");
            WriteTypePresence(w, "UnityEngine.UI.Text");
            WriteTypePresence(w, "TMPro.TextMeshProUGUI");
            WriteTypePresence(w, "TMPro.TMP_Text");

            _lastDumpPath = path;
            MelonLogger.Msg($"[Reimagined] Capabilities written: {path}");
            LogJson("devtools", "capabilities_written", new Dictionary<string, object?>
            {
                ["path"] = path
            });
            OverlayLog("dump", $"capabilities: {Path.GetFileName(path)}");
            Toast("Capabilities written", ToastKind.Ok);
        }

        private void DumpSceneUiProbe()
        {
            Directory.CreateDirectory(DumpsDir);
            var path = Path.Combine(DumpsDir, $"scene_ui_probe_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

            var scene = SceneManager.GetActiveScene();

            w.WriteLine("SMT3HD_Reimagined Scene UI Probe (scene-root traversal)");
            w.WriteLine($"ts={DateTime.Now:O}");
            w.WriteLine($"scene.name={scene.name}");
            w.WriteLine($"scene.buildIndex={scene.buildIndex}");
            w.WriteLine($"scene.isLoaded={scene.isLoaded}");
            w.WriteLine($"timescale={Time.timeScale}");
            w.WriteLine();

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                w.WriteLine($"Root objects: {roots.Count}");
                w.WriteLine();

                // Dump full hierarchy with component lists, capped to avoid massive files.
                int maxNodes = 6000;
                int nodeCount = 0;

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    DumpGameObjectRecursive(w, root, root.name, depth: 0, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes)
                    {
                        w.WriteLine();
                        w.WriteLine($"[TRUNCATED] Hit max node limit ({maxNodes}).");
                        break;
                    }
                }

                w.WriteLine();
                w.WriteLine("---- UI signature hints ----");
                w.WriteLine("Look for components containing: Canvas, EventSystem, UGUI, GraphicRaycaster, TMP, TextMeshPro, NGUI, UI*");
                w.WriteLine("If none appear, the game may be using a custom renderer/UI layer or instantiated later.");

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] Scene UI probe written: {path}");
                LogJson("devtools", "scene_ui_probe_written", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name,
                    ["root_count"] = roots.Count
                });
                OverlayLog("dump", $"scene_ui_probe: {Path.GetFileName(path)}");
                Toast("Scene UI probe written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                // Always leave a breadcrumb in the dump file itself so we can diff failures across scenes.
                w.WriteLine();
                w.WriteLine("---- EXCEPTION ----");
                w.WriteLine(ex.ToString());

                MelonLogger.Error($"[Reimagined] DumpSceneUiProbe exception (file still written): {ex}");
                LogJson("error", "scene_ui_probe_exception", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name,
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                _lastDumpPath = path;
                Toast("Scene UI probe failed", ToastKind.Error);
            }
        }

        private void DumpUiSignatureSummary()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"ui_signature_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                // We intentionally avoid relying on GameObject.GetComponents(Type) here because in IL2CPP
                // some bindings vary (System.Type vs Il2CppSystem.Type) and it’s easy to end up with “0 hits”.
                // Instead we scan the *hierarchy names/paths* which we already know are stable and informative.
                int maxNodes = 12000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                // Keyword counts (by *path/name*, not component type)
                string[] keys =
                {
                    "Canvas_UI",
                    "Main Canvas",
                    "RenderCanvas",
                    "EventSystem",
                    "UICamera",
                    "UI Camera",
                    "fieldUI",
                    "campUI",
                    "campUIBase",
                    "buttonguide",
                    "TextTM",
                    "Text1TM",
                    "Text2TM",
                    "Text_buttonTM",
                    "TMP UI SubObject",
                    "autoMapUI",
                    "mapUI",
                    "dialog",
                    "talk",
                    "battle",
                    "Hud",
                    "HUD",
                    "menu",
                };

                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in keys) counts[k] = 0;

                foreach (var kv in snap)
                {
                    var p = kv.Key;
                    foreach (var k in keys)
                    {
                        if (p.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                            counts[k]++;
                    }
                }

                // Derived “state” hints that we can trust even without components.
                bool pauseMenuActive = snap.TryGetValue("Canvas_UI/campUIBase/campUI", out var camp) && camp.ActiveHier;
                bool buttonGuideActive = snap.TryGetValue("Canvas_UI/buttonguide01/guide", out var guide) && guide.ActiveHier;
                bool fieldLocationVisible = snap.TryGetValue("Canvas_UI/fieldUI/flocation", out var floc) && floc.ActiveHier;
                bool moonVisible = snap.TryGetValue("Canvas_UI/fieldUI/moon", out var moon) && moon.ActiveHier;

                w.WriteLine("SMT3HD_Reimagined UI Signature Summary (name/path based)");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                w.WriteLine($"roots={roots.Count}");
                w.WriteLine($"nodes_scanned={nodeCount} (cap {maxNodes})");
                w.WriteLine();

                w.WriteLine("State hints:");
                w.WriteLine($"- pauseMenuActive(Canvas_UI/campUIBase/campUI.activeInHierarchy) = {pauseMenuActive}");
                w.WriteLine($"- buttonGuideActive(Canvas_UI/buttonguide01/guide.activeInHierarchy) = {buttonGuideActive}");
                w.WriteLine($"- fieldLocationVisible(Canvas_UI/fieldUI/flocation.activeInHierarchy) = {fieldLocationVisible}");
                w.WriteLine($"- moonVisible(Canvas_UI/fieldUI/moon.activeInHierarchy) = {moonVisible}");
                w.WriteLine();

                w.WriteLine("Counts by keyword (path contains keyword):");
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                    w.WriteLine($"- {kv.Key}: {kv.Value}");

                w.WriteLine();
                w.WriteLine("Examples (first 25 matches) for Text/TMP-ish nodes:");
                int shown = 0;
                foreach (var kv in snap)
                {
                    if (kv.Key.IndexOf("TextTM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        kv.Key.IndexOf("TMP UI SubObject", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        w.WriteLine($"- {kv.Key} [{kv.Value}]");
                        shown++;
                        if (shown >= 25) break;
                    }
                }

                w.WriteLine();
                w.WriteLine("Interpretation:");
                w.WriteLine("- If you see lots of Canvas_UI/fieldUI/... and TextTM/TMP nodes => Unity uGUI + TMP are present, even if component reflection is flaky.");
                w.WriteLine("- The pause menu in-field is primarily a *toggle* of Canvas_UI/campUIBase/campUI (exists in overworld but is inactive until pause).");
                w.WriteLine("- If scene.name stays the same across states, rely on these node toggles rather than SceneManager events.");

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] UI signature written: {path}");
                LogJson("devtools", "ui_signature_written", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name,
                    ["nodes_scanned"] = nodeCount,
                    ["pauseMenuActive"] = pauseMenuActive,
                });

                OverlayLog("dump", $"ui_signature: {Path.GetFileName(path)}");
                Toast("UI signature written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpUiSignatureSummary exception: {ex}");
                LogJson("error", "ui_signature_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                Toast("UI signature failed", ToastKind.Error);
            }
        }


        private static void DumpGameObjectRecursive(StreamWriter w, GameObject go, string path, int depth, ref int nodeCount, int maxNodes)
        {
            if (!IsAlive(go)) return;
            if (nodeCount >= maxNodes) return;
            nodeCount++;

            string indent = new string(' ', Math.Min(depth, 40) * 2);

            bool activeSelf = false;
            bool activeInHierarchy = false;
            int layer = -1;
            string tag = "";
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                activeSelf = go.activeSelf;
                activeInHierarchy = go.activeInHierarchy;
                layer = go.layer;
                tag = go.tag;
            }
            catch { /* ignore */ }

            w.WriteLine($"{indent}- {path}  [activeSelf={activeSelf} activeHier={activeInHierarchy} layer={layer} tag={tag}]");

            // Components (avoid generic GetComponents<T>())
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                foreach (var c in GetComponentsSafe(go))
                {
                    string tn;
                    try { tn = c.GetType().FullName ?? c.GetType().Name; }
                    catch { tn = "<type-error>"; }

                    if (!IsInterestingComponentTypeName(tn))
                        continue;

                    w.WriteLine($"{indent}    * {tn}");
                }
            }
            catch (Exception ex)
            {
                w.WriteLine($"{indent}    * <GetComponents failed: {ex.GetType().Name}: {ex.Message}>");
            }

            // Children
            Transform? t = null;
            try { t = go.transform; } catch { /* ignore */ }
            if (!IsAlive(t)) return;

            int childCount = 0;
            try { childCount = t!.childCount; } catch { /* ignore */ }

            for (int i = 0; i < childCount; i++)
            {
                if (nodeCount >= maxNodes) return;

                Transform child;
                try { child = t!.GetChild(i); }
                catch { continue; }

                if (!IsAlive(child))
                    continue;

                GameObject childGo;
                try { childGo = child.gameObject; }
                catch { continue; }

                if (!IsAlive(childGo))
                    continue;

                var childPath = $"{path}/{SafeName(childGo)}";
                DumpGameObjectRecursive(w, childGo, childPath, depth + 1, ref nodeCount, maxNodes);
            }
        }

        

        private void DumpUiDelta()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"ui_delta_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                int maxNodes = 12000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                var sig = BuildRootSignature(scene, roots);

                w.WriteLine("SMT3HD_Reimagined UI Delta (active state changes since last F6)");
                w.WriteLine($"root_signature={sig}");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                w.WriteLine($"roots={roots.Count}");
                w.WriteLine($"nodes_scanned={nodeCount} (cap {maxNodes})");
                w.WriteLine();

if (_lastUiSnapshot == null || _lastUiSnapshotSig == null)
{
    _lastUiSnapshot = snap;
    _lastUiSnapshotTs = DateTime.Now;
    _lastUiSnapshotSig = sig;

    w.WriteLine("No previous snapshot was stored.");
    w.WriteLine("This file captured the *baseline* snapshot; press F6 again after changing state (e.g., open pause) to get a delta.");
    _lastDumpPath = path;
    MelonLogger.Msg($"[Reimagined] UI delta baseline written: {path}");
    OverlayLog("dump", $"ui_delta_baseline: {Path.GetFileName(path)}");
    Toast("UI delta baseline written", ToastKind.Ok);
    return;
}

if (!string.Equals(_lastUiSnapshotSig, sig, StringComparison.Ordinal))
{
    w.WriteLine("Root signature changed since last snapshot.");
    w.WriteLine($"prev_root_signature={_lastUiSnapshotSig}");
    w.WriteLine("This file captured a *new baseline* for the current signature (to avoid noisy cross-context deltas).");
    w.WriteLine("Press F6 again after changing state *within this same context* to get a useful delta.");

    _lastUiSnapshot = snap;
    _lastUiSnapshotTs = DateTime.Now;
    _lastUiSnapshotSig = sig;

    _lastDumpPath = path;
    MelonLogger.Msg($"[Reimagined] UI delta baseline (sig changed) written: {path}");
    OverlayLog("dump", $"ui_delta_baseline_sig: {Path.GetFileName(path)}");
    Toast("UI delta baseline (sig changed)", ToastKind.Ok);
    return;
}

                var prev = _lastUiSnapshot;
                var changed = new List<(string path, UiNodeState before, UiNodeState after)>();
                var becameActive = new List<string>();
                var becameInactive = new List<string>();

                // Compare union of keys (some nodes may appear/disappear).
                var keys = new HashSet<string>(prev.Keys, StringComparer.Ordinal);
                keys.UnionWith(snap.Keys);

                foreach (var k in keys)
                {
                    prev.TryGetValue(k, out var a);
                    snap.TryGetValue(k, out var b);

                    // Missing nodes: treat as inactive.
                    bool aHier = prev.ContainsKey(k) ? a.ActiveHier : false;
                    bool bHier = snap.ContainsKey(k) ? b.ActiveHier : false;

                    bool aSelf = prev.ContainsKey(k) ? a.ActiveSelf : false;
                    bool bSelf = snap.ContainsKey(k) ? b.ActiveSelf : false;

                    if (aHier != bHier || aSelf != bSelf)
                    {
                        var before = prev.ContainsKey(k) ? a : new UiNodeState(false, false);
                        var after = snap.ContainsKey(k) ? b : new UiNodeState(false, false);
                        changed.Add((k, before, after));

                        if (!aHier && bHier) becameActive.Add(k);
                        if (aHier && !bHier) becameInactive.Add(k);
                    }
                }

                w.WriteLine($"changed_nodes={changed.Count}");
                w.WriteLine($"becameActive(activeHier False->True)={becameActive.Count}");
                w.WriteLine($"becameInactive(activeHier True->False)={becameInactive.Count}");
                w.WriteLine();

                // Summarize by second-level prefix (Canvas_UI/XYZ)
                var prefix = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var c in changed)
                {
                    var parts = c.path.Split('/');
                    var pfx = parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : c.path;
                    prefix.TryGetValue(pfx, out int v);
                    prefix[pfx] = v + 1;
                }

                w.WriteLine("Changed-node counts by subtree:");
                foreach (var kv in prefix.OrderByDescending(k => k.Value).Take(25))
                    w.WriteLine($"- {kv.Key}: {kv.Value}");
                w.WriteLine();

                w.WriteLine("Top 120 changes (sorted by path):");
                foreach (var c in changed.OrderBy(c => c.path, StringComparer.Ordinal).Take(120))
                    w.WriteLine($"- {c.path} | before[{c.before}] -> after[{c.after}]");

                _lastUiSnapshot = snap;
                _lastUiSnapshotTs = DateTime.Now;
                _lastUiSnapshotSig = sig;

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] UI delta written: {path}");
                LogJson("devtools", "ui_delta_written", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name,
                    ["changed_nodes"] = changed.Count,
                    ["becameActive"] = becameActive.Count,
                    ["becameInactive"] = becameInactive.Count
                });
                OverlayLog("dump", $"ui_delta: {Path.GetFileName(path)}");
                Toast("UI delta written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpUiDelta exception: {ex}");
                LogJson("error", "ui_delta_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                Toast("UI delta failed", ToastKind.Error);
            }
        }

        private void DumpUiWatchlist()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"ui_watchlist_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                int maxNodes = 12000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                var sig = BuildRootSignature(scene, roots);

                w.WriteLine("SMT3HD_Reimagined UI Watchlist");
                w.WriteLine($"root_signature={sig}");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                w.WriteLine($"roots={roots.Count}");
                w.WriteLine($"nodes_scanned={nodeCount} (cap {maxNodes})");
                w.WriteLine();
                w.WriteLine("Watch paths:");
                foreach (var p in UiWatchPaths)
                {
                    if (TryResolveSnapshotKey(snap, p, out var key) && snap.TryGetValue(key, out var st))
                        w.WriteLine($"- {key}: [{st}]");
                    else
                        w.WriteLine($"- {p}: (not found)");
                }

                w.WriteLine();
                w.WriteLine("Notes:");
                w.WriteLine("- Overworld vs pause can usually be distinguished by Canvas_UI/campUIBase/campUI.activeInHierarchy.");
                w.WriteLine("- Shop list open often flips shopUI/shoplist activeInHierarchy (and may hide shopUI/institutionUI).");
                w.WriteLine("- Save-terminal interactions tend to involve terminalUI/menu_tmnl and may disable terminalUI entirely when transitioning deeper.");
                w.WriteLine("- If a path is missing, it may be spawned later, renamed, or hidden under a different root in this state.");

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] UI watchlist written: {path}");
                OverlayLog("dump", $"ui_watchlist: {Path.GetFileName(path)}");
                Toast("UI watchlist written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpUiWatchlist exception: {ex}");
                Toast("UI watchlist failed", ToastKind.Error);
            }
        }



        private void DumpReflectionSurface()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"reflection_surface_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                w.WriteLine("[reflection_surface]");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine();

                DumpTypeMethodSurface(w, typeof(GameObject), "UnityEngine.GameObject",
                    new[] { "GetComponent", "GetComponents", "GetComponentsInChildren", "GetComponentInChildren", "GetComponentInParent" });

                DumpTypeMethodSurface(w, typeof(UnityEngine.Object), "UnityEngine.Object",
                    new[] { "Find", "FindObject", "FindObjects", "Instantiate", "Destroy" });

                w.WriteLine("NOTE:");
                w.WriteLine("  This file is meant to capture the exact managed method surface available in THIS runtime,");
                w.WriteLine("  so we can stop guessing about IL2CPP bridge signatures (especially around GetComponents).");
                w.WriteLine();

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] Wrote reflection surface: {path}");
                OverlayLog("dump", $"reflection_surface: {Path.GetFileName(path)}");
                Toast("Reflection surface written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpReflectionSurface exception: {ex}");
                Toast("Reflection surface failed", ToastKind.Error);
            }
        }

                static void DumpTypeMethodSurface(StreamWriter w, Type t, String label, String[] prefixes)
        {
            w.WriteLine($"[{label}]");
            w.WriteLine($"type={t.FullName}");
            w.WriteLine($"prefixes={string.Join(",", prefixes)}");
            w.WriteLine();

            MethodInfo[] all;
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                all = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                w.WriteLine($"<GetMethods failed: {ex.GetType().Name}: {ex.Message}>");
                w.WriteLine();
                return;
            }

            var methods = all
                .Where(mi => prefixes.Any(p => mi.Name.StartsWith(p, StringComparison.Ordinal)))
                .OrderBy(mi => mi.Name, StringComparer.Ordinal);

            foreach (var mi in methods)
            {
                // Some IL2CPP bridge methods have signatures that throw when reflected (generic constraint issues).
                // Keep this dump best-effort and never abort the whole file.
                w.WriteLine(FormatMethodSig(mi));
            }

            w.WriteLine();
        }

                static string FormatMethodSig(MethodInfo mi)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var ps = mi.GetParameters();
                var psStr = string.Join(", ", ps.Select(p =>
                {
                    string pt;
                    try { pt = p.ParameterType.FullName ?? p.ParameterType.Name; }
                    catch { pt = "<param-type-error>"; }
                    return $"{pt} {p.Name}";
                }));

                string ret;
                try { ret = mi.ReturnType.FullName ?? mi.ReturnType.Name; }
                catch { ret = "<ret-type-error>"; }

                string decl;
                try { decl = mi.DeclaringType?.FullName ?? mi.DeclaringType?.Name ?? "<no-declaring-type>"; }
                catch { decl = "<decl-type-error>"; }

                return $"{ret} {decl}.{mi.Name}({psStr})";
            }
            catch (Exception ex)
            {
                string decl;
                try { decl = mi.DeclaringType?.FullName ?? mi.DeclaringType?.Name ?? "<no-declaring-type>"; }
                catch { decl = "<decl-type-error>"; }

                return $"<sig_unavailable {ex.GetType().Name}> {decl}.{mi.Name}(...)";
            }
        }

        private void DumpUiAnchorComponents()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"ui_anchor_components_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                int maxNodes = 16000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                var sig = BuildRootSignature(scene, roots);

                w.WriteLine("SMT3HD_Reimagined UI Anchor Components (focused)");
                w.WriteLine($"root_signature={sig}");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                w.WriteLine($"roots={roots.Count}");
                w.WriteLine($"nodes_scanned={nodeCount} (cap {maxNodes})");
                w.WriteLine();

                // Anchors: prefer Canvas_UI children (stable across contexts), but also allow top-level roots (shopUI/terminalUI)
                // so we still find them if they are exposed as separate fclUI roots.
                var anchorQueries = new List<string>
                {
                    "Canvas_UI",
                    "Canvas_UI/fieldUI",
                    "Canvas_UI/campUIBase/campUI",
                    "Canvas_UI/shopUI",
                    "Canvas_UI/terminalUI",
                    "Canvas_UI/saveUI(Clone)",

                    // root-level variants sometimes appear when fclUI returns them as independent roots
                    "shopUI",
                    "terminalUI",
                };

                // Dynamic saveUI clone discovery (name can be saveUI(Clone) and can exist even if not in watchlist).
                var discoveredSave = FindFirstKeyByLeafPrefix(snap, "saveUI");
                if (!string.IsNullOrEmpty(discoveredSave) && !anchorQueries.Contains(discoveredSave))
                    anchorQueries.Add(discoveredSave!);

                w.WriteLine("Anchors (resolved from current snapshot):");
                w.WriteLine();

                foreach (var q in anchorQueries)
                {
                    if (!TryResolveSnapshotKey(snap, q, out var key))
                    {
                        w.WriteLine($"== {q} == (not found)");
                        w.WriteLine();
                        continue;
                    }

                    w.WriteLine($"== {key} ==");
                    w.WriteLine($"state: [{snap[key]}]");

                    // Try to find the live GameObject for this snapshot path so we can list *all* components.
                    var go = TryFindGameObjectBySnapshotPath(roots, key);
                    if (!IsAlive(go))
                    {
                        w.WriteLine("live_object: <not resolved>");
                        w.WriteLine();
                        continue;
                    }

                    var goLive = go!;
                    w.WriteLine($"live_object: {SafeName(goLive)} (activeSelf={SafeBool(() => goLive.activeSelf)} activeHier={SafeBool(() => goLive.activeInHierarchy)})");
                    w.WriteLine("components:");
                    try
                    {
                        var comps = GetComponentsSafe(goLive, w);
                        if (comps.Length == 0)
                        {
                            w.WriteLine("  (none)");
                        }
                        else
                        {
                            // NOTE: Do NOT call the generic GetComponents<T>() here.
                            // In this project's Unity/MelonLoader reference surface, GameObject exposes a non-generic GetComponents(...)
                            // and attempting GetComponents<Component>() fails to compile (CS0308).
                            // We use reflection below to call GetComponents(Type) instead.
                            foreach (var c in comps)
                            {
                                try
                                {
                                    string managedType;
                                    string? refHint;
                                    var display = FormatComponentTypeForDump(c, out refHint, out managedType);
                                    if (!string.IsNullOrEmpty(refHint))
                                        w.WriteLine($"  - {display}  ({refHint})");
                                    else
                                        w.WriteLine($"  - {display}");
                                }
                                catch (Exception cex)
                                {
                                    w.WriteLine($"  - <component dump failed: {cex.GetType().Name}: {cex.Message}>");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        w.WriteLine($"  <GetComponents failed: {ex.GetType().Name}: {ex.Message}>");
                    }

                    // Children preview (helps spot which sub-object owns the interesting script)
                    try
                    {
                        var t = goLive.transform;
                        int cc = t.childCount;
                        w.WriteLine($"children: {cc}");
                        int take = Math.Min(cc, 60);
                        for (int i = 0; i < take; i++)
                        {
                            var child = t.GetChild(i);
                            var cgo = child.gameObject;
                            if (!IsAlive(cgo))
                            {
                                w.WriteLine("  - <dead child>");
                                continue;
                            }
                            var cgoLive = cgo!;
                            w.WriteLine($"  - {SafeName(cgoLive)} [activeSelf={SafeBool(() => cgoLive.activeSelf)} activeHier={SafeBool(() => cgoLive.activeInHierarchy)}]");
                        }
                        if (cc > take)
                            w.WriteLine($"  ... ({cc - take} more)");
                    }
                    catch
                    {
                        w.WriteLine("children: <unavailable>");
                    }


                    w.WriteLine("Descendant name matches (heuristic):");
                    WriteDescendantNameMatches(
                        w,
                        goLive,
                        new[] { "msg", "tmnl", "cmd", "select", "panel", "list", "help", "cursor", "slot", "text", "Text", "TMP", "btn", "button" },
                        maxMatches: 60,
                        maxDepth: 12
                    );

                    w.WriteLine();
                }

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] UI anchor components written: {path}");
                LogJson("devtools", "ui_anchor_components_written", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name
                });

                OverlayLog("dump", $"ui_anchor_components: {Path.GetFileName(path)}");
                Toast("UI anchor components written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpUiAnchorComponents exception: {ex}");
                LogJson("error", "ui_anchor_components_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                Toast("UI anchor components failed", ToastKind.Error);
            }
        }


        private void DumpUiComponentInventory()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                Directory.CreateDirectory(DumpsDir);
                var path = Path.Combine(DumpsDir, $"ui_component_inventory_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                using var w = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 128);

                int maxNodes = 24000;
                int nodeCount = 0;
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);

                foreach (var root in roots.OrderBy(r => r.name))
                {
                    SnapshotObjectRecursive(root, root.name, snap, ref nodeCount, maxNodes);
                    if (nodeCount >= maxNodes) break;
                }

                var sig = BuildRootSignature(scene, roots);

                w.WriteLine("SMT3HD_Reimagined UI Component Inventory (anchor subtree)");
                w.WriteLine($"root_signature={sig}");
                w.WriteLine($"ts={DateTime.Now:O}");
                w.WriteLine($"scene={scene.name} ({scene.buildIndex})");
                w.WriteLine($"roots={roots.Count}");
                w.WriteLine($"nodes_scanned={nodeCount} (cap {maxNodes})");
                w.WriteLine();

                var anchorQueries = new List<string>
                {
                    "Canvas_UI",
                    "Canvas_UI/fieldUI",
                    "Canvas_UI/campUIBase/campUI",
                    "Canvas_UI/shopUI",
                    "Canvas_UI/terminalUI",
                    "Canvas_UI/saveUI(Clone)",
                    "shopUI",
                    "terminalUI",
                };

                var discoveredSave = FindFirstKeyByLeafPrefix(snap, "saveUI");
                if (!string.IsNullOrEmpty(discoveredSave) && !anchorQueries.Contains(discoveredSave))
                    anchorQueries.Add(discoveredSave!);

                foreach (var q in anchorQueries)
                {
                    if (!TryResolveSnapshotKey(snap, q, out var key))
                    {
                        w.WriteLine($"== {q} == (not found)");
                        w.WriteLine();
                        continue;
                    }

                    var go = TryFindGameObjectBySnapshotPath(roots, key);
                    if (!IsAlive(go))
                    {
                        w.WriteLine($"== {key} == (live not resolved)");
                        w.WriteLine();
                        continue;
                    }

                    var rootGo = go!;
                    w.WriteLine($"== {key} ==");
                    w.WriteLine($"live_object: {SafeName(rootGo)} (activeSelf={SafeBool(() => rootGo.activeSelf)} activeHier={SafeBool(() => rootGo.activeInHierarchy)})");
                    w.WriteLine();

                    int scannedNodes = 0;
                    int scannedComps = 0;

                    var typeCounts = new Dictionary<string, (int count, string examplePath)>(StringComparer.Ordinal);

                    var stack = new Stack<(Transform t, string path)>();
                    stack.Push((rootGo.transform, key));

                    int nodeCap = 8000; // per-anchor subtree cap to avoid runaway
                    while (stack.Count > 0 && scannedNodes < nodeCap)
                    {
                        var (t, p) = stack.Pop();
                        var g = t.gameObject;
                        if (!IsAlive(g))
                            continue;

                        scannedNodes++;

                        Component[] comps;
                        try { comps = GetComponentsSafe(g); }
                        catch { comps = Array.Empty<Component>(); }

                        foreach (var c in comps)
                        {
                            scannedComps++;
                            string managedType;
                            string? refHint;
                            var display = FormatComponentTypeForDump(c, out refHint, out managedType);

                            if (typeCounts.TryGetValue(display, out var cur))
                                typeCounts[display] = (cur.count + 1, cur.examplePath);
                            else
                                typeCounts[display] = (1, p);
                        }

                        // Push children
                        try
                        {
                            int cc = t.childCount;
                            for (int i = 0; i < cc; i++)
                            {
                                var child = t.GetChild(i);
                                var cgo = child.gameObject;
                                if (!IsAlive(cgo)) continue;
                                stack.Push((child, p + "/" + SafeName(cgo!)));
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    w.WriteLine($"subtree_nodes_scanned={scannedNodes} (cap {nodeCap})");
                    w.WriteLine($"components_scanned={scannedComps}");
                    w.WriteLine($"unique_component_types={typeCounts.Count}");
                    w.WriteLine();

                    w.WriteLine("Top component types by count:");
                    foreach (var kv in typeCounts.OrderByDescending(k => k.Value.count).ThenBy(k => k.Key).Take(80))
                    {
                        w.WriteLine($"  {kv.Value.count,4}  {kv.Key}  (e.g. {kv.Value.examplePath})");
                    }

                    w.WriteLine();
                    w.WriteLine("Interesting component types (UI-ish + Assembly-CSharp):");
                    foreach (var kv in typeCounts
                        .Where(kv => IsInterestingComponentTypeName(kv.Key) || kv.Key.StartsWith("Assembly-CSharp::", StringComparison.Ordinal))
                        .OrderByDescending(k => k.Value.count)
                        .ThenBy(k => k.Key)
                        .Take(120))
                    {
                        w.WriteLine($"  {kv.Value.count,4}  {kv.Key}  (e.g. {kv.Value.examplePath})");
                    }

                    w.WriteLine();
                }

                _lastDumpPath = path;
                MelonLogger.Msg($"[Reimagined] UI component inventory written: {path}");
                LogJson("devtools", "ui_component_inventory_written", new Dictionary<string, object?>
                {
                    ["path"] = path,
                    ["scene"] = scene.name
                });
                OverlayLog("dump", $"ui_component_inventory: {Path.GetFileName(path)}");
                Toast("UI component inventory written", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Reimagined] DumpUiComponentInventory exception: {ex}");
                LogJson("error", "ui_component_inventory_exception", new Dictionary<string, object?>
                {
                    ["message"] = ex.Message,
                    ["stack"] = ex.ToString()
                });
                Toast("UI component inventory failed", ToastKind.Error);
            }
        }


        private static string? FindFirstKeyByLeafPrefix(Dictionary<string, UiNodeState> snap, string leafPrefix)
        {
            if (string.IsNullOrEmpty(leafPrefix))
                return null;

            // Prefer paths under Canvas_UI when present.
            string? best = null;

            foreach (var k in snap.Keys)
            {
                // fast reject
                if (k.Length < leafPrefix.Length)
                    continue;

                int slash = k.LastIndexOf('/');
                string leaf = slash >= 0 ? k.Substring(slash + 1) : k;

                if (leaf.StartsWith(leafPrefix, StringComparison.Ordinal))
                {
                    if (best == null)
                        best = k;
                    else
                    {
                        // prefer Canvas_UI/...
                        if (best.StartsWith("Canvas_UI/", StringComparison.Ordinal) == false &&
                            k.StartsWith("Canvas_UI/", StringComparison.Ordinal))
                            best = k;
                    }
                }
            }

            return best;
        }

        private static bool TryResolveSnapshotKey(Dictionary<string, UiNodeState> snap, string desired, out string resolved)
        {
            resolved = desired;

            if (snap.ContainsKey(desired))
                return true;

            // Try clone suffix
            if (!desired.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                var clone = desired + "(Clone)";
                if (snap.ContainsKey(clone))
                {
                    resolved = clone;
                    return true;
                }
            }
            else
            {
                var noClone = desired.Replace("(Clone)", "");
                if (snap.ContainsKey(noClone))
                {
                    resolved = noClone;
                    return true;
                }
            }

            // Try adding/removing Canvas_UI prefix (helps when fclUI returns shopUI/terminalUI as a root)
            const string pfx = "Canvas_UI/";
            if (desired.StartsWith(pfx, StringComparison.Ordinal))
            {
                var alt = desired.Substring(pfx.Length);
                if (snap.ContainsKey(alt))
                {
                    resolved = alt;
                    return true;
                }
                if (!alt.EndsWith("(Clone)", StringComparison.Ordinal) && snap.ContainsKey(alt + "(Clone)"))
                {
                    resolved = alt + "(Clone)";
                    return true;
                }
            }
            else
            {
                var alt = pfx + desired;
                if (snap.ContainsKey(alt))
                {
                    resolved = alt;
                    return true;
                }
                if (!alt.EndsWith("(Clone)", StringComparison.Ordinal) && snap.ContainsKey(alt + "(Clone)"))
                {
                    resolved = alt + "(Clone)";
                    return true;
                }
            }

            // Leaf-prefix fallback: resolve by last-segment match (handles saveUI(Clone) when you asked for saveUI)
            int slash = desired.LastIndexOf('/');
            var leaf = slash >= 0 ? desired.Substring(slash + 1) : desired;
            foreach (var k in snap.Keys)
            {
                int ks = k.LastIndexOf('/');
                var kleaf = ks >= 0 ? k.Substring(ks + 1) : k;
                if (kleaf.Equals(leaf, StringComparison.Ordinal) ||
                    kleaf.Equals(leaf + "(Clone)", StringComparison.Ordinal) ||
                    kleaf.StartsWith(leaf + "(", StringComparison.Ordinal))
                {
                    resolved = k;
                    return true;
                }
            }

            return false;
        }

        private static GameObject? TryFindGameObjectBySnapshotPath(List<GameObject> roots, string snapshotPath)
        {
            if (roots == null || roots.Count == 0)
                return null;

            // snapshotPath is like "Canvas_UI/saveUI(Clone)/savelist"
            var parts = snapshotPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            // Find matching root first
            GameObject? cur = null;
            foreach (var r in roots)
            {
                if (!IsAlive(r)) continue;
                string rn = SafeName(r);
                if (string.Equals(rn, parts[0], StringComparison.Ordinal))
                {
                    cur = r;
                    break;
                }
            }

            if (!IsAlive(cur))
            {
                // Sometimes the "root" is not in roots; try global find for first segment.
                try { cur = GameObject.Find(parts[0]); } catch { cur = null; }
            }

            if (!IsAlive(cur))
                return null;

            for (int i = 1; i < parts.Length; i++)
            {
                var want = parts[i];
                Transform? t = null;
                try { t = cur!.transform; } catch { t = null; }
                if (!IsAlive(t))
                    return null;

                GameObject? next = null;
                int cc = 0;
                try { cc = t!.childCount; } catch { cc = 0; }

                for (int c = 0; c < cc; c++)
                {
                    Transform ct;
                    try { ct = t!.GetChild(c); } catch { continue; }
                    GameObject cgo;
                    try { cgo = ct.gameObject; } catch { continue; }

                    if (!IsAlive(cgo))
                        continue;

                    var name = SafeName(cgo);
                    if (string.Equals(name, want, StringComparison.Ordinal))
                    {
                        next = cgo;
                        break;
                    }

                    // allow clone tolerance when the snapshotPath includes "(Clone)" but we encounter a stripped name or vice versa
                    if (want.EndsWith("(Clone)", StringComparison.Ordinal))
                    {
                        var baseName = want.Replace("(Clone)", "");
                        if (string.Equals(name, baseName, StringComparison.Ordinal))
                        {
                            next = cgo;
                            break;
                        }
                    }
                    else
                    {
                        if (string.Equals(name, want + "(Clone)", StringComparison.Ordinal))
                        {
                            next = cgo;
                            break;
                        }
                    }
                }

                if (!IsAlive(next))
                    return null;

                cur = next;
            }

            return cur;
        }

        private static bool SafeBool(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }




private void ClearUiSnapshots()
{
    _lastUiSnapshot = null;
    _lastUiSnapshotSig = null;
    _lastUiSnapshotTs = default;
    MelonLogger.Msg("[Reimagined] UI snapshot cleared. Next F6 will write a baseline for the current root signature.");
}


private static string BuildRootSignature(Scene scene, List<GameObject> roots)
{
    // Keep this stable across frames: just the scene name + sorted root names we decided to scan.
    var names = new List<string>(roots.Count);
    foreach (var r in roots)
    {
        if (!IsAlive(r)) continue;
        try { names.Add(r.name ?? "<noname>"); }
        catch { names.Add("<name-error>"); }
    }
    names.Sort(StringComparer.Ordinal);
    return scene.name + "|" + string.Join("|", names);
}

        private static void SnapshotObjectRecursive(GameObject go, string path, Dictionary<string, UiNodeState> snap, ref int nodeCount, int maxNodes)
        {
            if (go == null) return;
            if (nodeCount >= maxNodes) return;

            bool activeSelf = false;
            bool activeHier = false;

            try { activeSelf = go.activeSelf; } catch { /* ignore */ }
            try { activeHier = go.activeInHierarchy; } catch { /* ignore */ }

            snap[path] = new UiNodeState(activeSelf, activeHier);
            nodeCount++;

            Transform? t = null;
            try { t = go.transform; } catch { t = null; }
            if (t == null) return;

            int childCount = 0;
            try { childCount = t.childCount; } catch { childCount = 0; }

            for (int i = 0; i < childCount; i++)
            {
                Transform? ct = null;
                try { ct = t.GetChild(i); } catch { ct = null; }
                if (ct == null) continue;

                GameObject? cgo = null;
                try { cgo = ct.gameObject; } catch { cgo = null; }
                if (cgo == null) continue;

                var cpath = path + "/" + cgo.name;
                SnapshotObjectRecursive(cgo, cpath, snap, ref nodeCount, maxNodes);
                if (nodeCount >= maxNodes) break;
            }
        }

private static void ScanObjectRecursive(GameObject go, string[] keys, Dictionary<string, int> counts, ref int nodeCount, int maxNodes)
{
    if (!IsAlive(go)) return;
    if (nodeCount >= maxNodes) return;
    nodeCount++;

    try
    {
        foreach (var c in GetComponentsSafe(go))
        {
            var tn = c.GetType().FullName ?? c.GetType().Name;

            foreach (var k in keys)
            {
                if (tn.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    counts.TryGetValue(k, out var v);
                    counts[k] = v + 1;
                }
            }
        }
    }
    catch
    {
        // ignore
    }

    Transform? t = null;
    try { t = go.transform; } catch { /* ignore */ }
    if (!IsAlive(t)) return;

    int childCount = 0;
    try { childCount = t!.childCount; } catch { /* ignore */ }

    for (int i = 0; i < childCount; i++)
    {
        if (nodeCount >= maxNodes) return;

        Transform child;
        try { child = t!.GetChild(i); }
        catch { continue; }

        if (!IsAlive(child))
            continue;

        GameObject childGo;
        try { childGo = child.gameObject; }
        catch { continue; }

        if (!IsAlive(childGo))
            continue;

        ScanObjectRecursive(childGo, keys, counts, ref nodeCount, maxNodes);
    }
}

        private static void WriteTypePresence(StreamWriter w, string typeName)
        {
            var t = FindTypeInLoadedAssemblies(typeName);
            w.WriteLine($"- {typeName}: {(t != null ? "YES" : "no")}");
        }

        private static Type? FindTypeInLoadedAssemblies(string typeName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = a.GetType(typeName, throwOnError: false, ignoreCase: false);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        

private static MethodInfo? FindStaticMethod(Type t, string name, Type returnType)
{
    return FindStaticMethod(t, name, returnType, Array.Empty<Type>());
}

private static MethodInfo? FindStaticMethod(Type t, string name, Type returnType, params Type[] paramTypes)
{
    if (paramTypes == null) paramTypes = Array.Empty<Type>();

    // Reflection helper: find a static method by name + return type + parameter types.
    // Used when IL2CPP bridge method overloads share names but differ by signature.
    try
    {
        var all = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var mi in all)
        {
            if (!string.Equals(mi.Name, name, StringComparison.Ordinal))
                continue;

            // Return type match (void included).
            Type rt;
            try { rt = mi.ReturnType; }
            catch { continue; }

            if (rt != returnType)
                continue;

            ParameterInfo[] ps;
            try { ps = mi.GetParameters(); }
            catch { continue; }

            int wantN = paramTypes.Length;
            if (ps.Length != wantN)
                continue;

            bool ok = true;
            for (int i = 0; i < wantN; i++)
            {
                Type pt;
                try { pt = ps[i].ParameterType; }
                catch { ok = false; break; }

                var want = paramTypes[i];

                if (want == null || pt != want)
                {
                    ok = false;
                    break;
                }
            }

            if (!ok)
                continue;

            return mi;
        }
    }
    catch
    {
    }

    return null;
}


private static string GetInputStringBestEffort()
{
    // Some Unity / IL2CPP bindings used by SMT3HD don't expose Input.inputString at compile-time.
    // We grab it via reflection when available; otherwise return "".
    try
    {
        var tInput = typeof(UnityEngine.Input);

        var prop = tInput.GetProperty("inputString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null && prop.PropertyType == typeof(string))
        {
            return (string)(prop.GetValue(null, null) ?? string.Empty);
        }

        var field = tInput.GetField("inputString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null && field.FieldType == typeof(string))
        {
            return (string)(field.GetValue(null) ?? string.Empty);
        }
    }
    catch
    {
        // ignore
    }

    return string.Empty;
}

private static MethodInfo? FindStaticMethod(Type t, string name, int paramCount)
        {
            // Reflection helper: find a static method by name + parameter count.
            // Keep this best-effort because IL2CPP bridge metadata can be incomplete or throw on reflection.
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var all = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var mi in all)
                {
                    if (!string.Equals(mi.Name, name, StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] ps;
                    try { ps = mi.GetParameters(); }
                    catch { continue; }

                    if (ps.Length != paramCount)
                        continue;

                    return mi;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }


        // --------------------------
        // Safe Unity helpers (IL2CPP / stripped builds friendly)
        // --------------------------

        private static bool IsAlive(UnityEngine.Object? o)
        {
            // Unity overrides == to return true when native object is destroyed.
            return o != null;
        }

        private static string SafeName(UnityEngine.Object? o)
        {
            if (!IsAlive(o)) return "<null>";
            try { return o!.name ?? "<noname>"; }
            catch { return "<name-error>"; }
        }

        private static int SafeInstanceId(UnityEngine.Object? o)
        {
            if (!IsAlive(o)) return 0;
            try { return o!.GetInstanceID(); }
            catch { return 0; }
        }

        

        private static string SafeManagedTypeName(object? o)
        {
            if (o == null) return "<null>";
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var t = o.GetType();
                return t.FullName ?? t.Name ?? "<type>";
            }
            catch
            {
                return "<type-error>";
            }
        }

        private static string? TryGetIl2CppNativeFullName(object? obj)
        {
            // Best-effort: ask Il2CppInterop's IL2CPP API for the native class name/namespace for this object.
            // This avoids the common situation where managed wrapper types collapse to UnityEngine.Component.
            if (obj == null) return null;

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var il2cpp = FindLoadedTypeByFullName("Il2CppInterop.Runtime.IL2CPP") 
                         ?? FindLoadedTypeByFullName("Il2CppInterop.Runtime.IL2CPP+IL2CPP") // some builds nest
                         ?? FindLoadedTypeBySimpleName("IL2CPP");
                if (il2cpp == null)
                    return null;

                // Try to get native object pointer from the Il2Cpp object wrapper.
                // Common helper: IL2CPP.Il2CppObjectBaseToPtrNotNull(object)
                IntPtr objPtr = IntPtr.Zero;
                foreach (var methName in new[] { "Il2CppObjectBaseToPtrNotNull", "Il2CppObjectBaseToPtr", "ManagedObjectToIl2Cpp" })
                {
                    var mi = il2cpp.GetMethod(methName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (mi == null) continue;

                    try
                    {
                        var v = mi.Invoke(null, new[] { obj });
                        if (v is IntPtr ip && ip != IntPtr.Zero) { objPtr = ip; break; }
                        // Some builds return nint boxed as Int64 on net6
                        if (v is long l && l != 0) { objPtr = new IntPtr(l); break; }
                        if (v is nint ni && ni != 0) { objPtr = (IntPtr)ni; break; }
                    }
                    catch
                    {
                        // continue
                    }
                }

                if (objPtr == IntPtr.Zero)
                    return null;

                // il2cpp_object_get_class(IntPtr obj) -> IntPtr
                var miGetClass = il2cpp.GetMethod("il2cpp_object_get_class", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (miGetClass == null)
                    return null;

                var klassObj = miGetClass.Invoke(null, new object[] { objPtr });
                IntPtr klass = klassObj is IntPtr ik ? ik : (klassObj is long lk ? new IntPtr(lk) : IntPtr.Zero);
                if (klass == IntPtr.Zero)
                    return null;

                // il2cpp_class_get_name / il2cpp_class_get_namespace -> IntPtr(char*)
                string name = "";
                string ns = "";

                var miGetName = il2cpp.GetMethod("il2cpp_class_get_name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (miGetName != null)
                {
                    var p = miGetName.Invoke(null, new object[] { klass });
                    IntPtr ip = p is IntPtr ipp ? ipp : (p is long lp ? new IntPtr(lp) : IntPtr.Zero);
                    if (ip != IntPtr.Zero)
                        name = Marshal.PtrToStringAnsi(ip) ?? "";
                }

                var miGetNs = il2cpp.GetMethod("il2cpp_class_get_namespace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (miGetNs != null)
                {
                    var p = miGetNs.Invoke(null, new object[] { klass });
                    IntPtr ip = p is IntPtr ipp ? ipp : (p is long lp ? new IntPtr(lp) : IntPtr.Zero);
                    if (ip != IntPtr.Zero)
                        ns = Marshal.PtrToStringAnsi(ip) ?? "";
                }

                if (string.IsNullOrEmpty(name))
                    return null;

                if (string.IsNullOrEmpty(ns))
                    return name; // Assembly-CSharp types often have empty namespace (e.g., saveUI)

                return $"{ns}.{name}";
            }
            catch
            {
                return null;
            }
        }

        private static string FormatComponentTypeForDump(object comp, out string? refHint, out string managedType)
        {
            managedType = SafeManagedTypeName(comp);
            refHint = null;

            var native = TryGetIl2CppNativeFullName(comp);

            // If native type has no namespace (common for Assembly-CSharp), give it a helpful label and ref hint.
            if (!string.IsNullOrEmpty(native))
            {
                if (native.IndexOf('.') < 0)
                {
                    // Treat as Assembly-CSharp global-ns type; reference folder keeps these under .../Il2Cpp/<Type>.cs
                    refHint = $"tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/{native}.cs";
                    return $"Assembly-CSharp::{native}";
                }

                // If it looks like Il2Cpp.<Type>, we can also provide a ref hint.
                if (native.StartsWith("Il2Cpp.", StringComparison.Ordinal))
                {
                    var shortName = native.Substring("Il2Cpp.".Length);
                    refHint = $"tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/{shortName}.cs";
                }

                return native;
            }

            // Fallback: managed wrapper type
            if (managedType.StartsWith("Il2Cpp.", StringComparison.Ordinal))
            {
                var shortName = managedType.Substring("Il2Cpp.".Length);
                refHint = $"tools/references/Assembly-CSharp/Assembly-CSharp/Il2Cpp/{shortName}.cs";
            }

            return managedType;
        }

        private static bool IsInterestingComponentTypeName(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return false;

            if (typeFullName == "UnityEngine.Component")
                return false;

            // Keep the scene UI probe output manageable: only print UI-ish / rendering-ish components.
            // (The signature dump can still scan everything.)
            var s = typeFullName;

            // Common UI & event plumbing
            if (s.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("EventSystem", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("Raycaster", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("UnityEngine.UI", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Text / sprite / image / renderer-y
            if (s.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("Renderer", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Cameras / animations sometimes own UI rigs
            if (s.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

                private static Component[] GetComponentsSafe(GameObject go)
        {
            return GetComponentsSafe(go, null);
        }

        private static Component[] GetComponentsSafe(GameObject go, TextWriter? diag)
        {
            if (!IsAlive(go))
            {
                diag?.WriteLine("  <go not alive>");
                return Array.Empty<Component>();
            }

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var tGo = go.GetType();

                // In IL2CPP MelonLoader builds, GetComponents overloads can take either System.Type or Il2CppSystem.Type
                // depending on the binding generation. We reflect for both, then coerce the return to Component[] best-effort.
                var cands = tGo.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "GetComponents")
                    .Where(m =>
                    {
                        try { return m.GetParameters().Length == 1; }
                        catch { return false; }
                    })
                    .ToArray();

                if (cands.Length == 0)
                {
                    diag?.WriteLine("  <no GetComponents overloads with 1 param>");
                    return Array.Empty<Component>();
                }

                foreach (var m in cands)
                {
                    ParameterInfo[] ps;
                    try { ps = m.GetParameters(); }
                    catch (Exception ex)
                    {
                        diag?.WriteLine($"  <GetParameters failed for {m.Name}: {ex.GetType().Name}: {ex.Message}>");
                        continue;
                    }

                    if (ps.Length != 1)
                        continue;

                    var p0 = ps[0].ParameterType;
                    object? arg = null;

                    if (p0 == typeof(Type))
                    {
                        arg = typeof(Component);
                    }
                    else if (string.Equals(p0.FullName, "Il2CppSystem.Type", StringComparison.Ordinal))
                    {
                        arg = TryMakeIl2CppSystemType(typeof(Component), diag);
                        if (arg == null)
                        {
                            diag?.WriteLine("  <failed to create Il2CppSystem.Type for Component>");
                            continue;
                        }
                    }
                    else
                    {
                        // Not a compatible overload for our use.
                        continue;
                    }

                    object? res = null;
                    try
                    {
                        res = m.Invoke(go, new object?[] { arg });
                    }
                    catch (TargetInvocationException tie)
                    {
                        var ie = tie.InnerException;
                        diag?.WriteLine($"  <invoke {m.Name}({p0.FullName}) threw {ie?.GetType().Name}: {ie?.Message}>");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        diag?.WriteLine($"  <invoke {m.Name}({p0.FullName}) threw {ex.GetType().Name}: {ex.Message}>");
                        continue;
                    }

                    var comps = CoerceToComponents(res, diag);
                    diag?.WriteLine($"  <GetComponents used {m.Name}({p0.FullName}) -> {DescribeObject(res)} -> count={comps.Length}>");

                    if (comps.Length > 0)
                        return comps;
                }

                diag?.WriteLine("  <no compatible GetComponents overload produced any Component items>");
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"  <GetComponentsSafe exception {ex.GetType().Name}: {ex.Message}>");
            }

            return Array.Empty<Component>();
        }

        private static string DescribeObject(object? o)
        {
            if (o == null) return "null";
            try { return o.GetType().FullName ?? o.GetType().Name; }
            catch { return "<type-error>"; }
        }

        private static Component[] CoerceToComponents(object? res, TextWriter? diag)
        {
            if (res == null)
                return Array.Empty<Component>();

            if (res is Component[] arr)
                return arr.Where(c => c != null).ToArray();

            // Most Il2Cpp array wrappers implement IEnumerable; iterate and collect Component values best-effort.
            if (res is System.Collections.IEnumerable en)
            {
                var list = new List<Component>();
                try
                {
                    foreach (var item in en)
                    {
                        if (item is Component c && c != null)
                            list.Add(c);
                    }
                }
                catch (Exception ex)
                {
                    diag?.WriteLine($"  <enumerating GetComponents result failed: {ex.GetType().Name}: {ex.Message}>");
                }
                return list.ToArray();
            }

            // As a last resort, try System.Array
            if (res is Array a)
            {
                var list = new List<Component>();
                try
                {
                    foreach (var item in a)
                    {
                        if (item is Component c && c != null)
                            list.Add(c);
                    }
                }
                catch (Exception ex)
                {
                    diag?.WriteLine($"  <iterating Array GetComponents result failed: {ex.GetType().Name}: {ex.Message}>");
                }
                return list.ToArray();
            }

            diag?.WriteLine($"  <GetComponents returned non-enumerable: {DescribeObject(res)}>");
            return Array.Empty<Component>();
        }

        private static object? TryMakeIl2CppSystemType(Type sysType, TextWriter? diag)
        {
            // Preferred: Il2CppInterop.Runtime.Il2CppType.From(System.Type) or Il2CppInterop.Runtime.Il2CppType.Of<T>()
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var il2cppType = FindLoadedTypeByFullName("Il2CppInterop.Runtime.Il2CppType")
                                 ?? FindLoadedTypeBySimpleName("Il2CppType");

                if (il2cppType != null)
                {
                    // From(Type)
                    var from = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "From") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                            }
                            catch { return false; }
                        });

                    if (from != null)
                    {
                        try
                        {
                            var v = from.Invoke(null, new object?[] { sysType });
                            if (v != null) return v;
                        }
                        catch (Exception ex)
                        {
                            diag?.WriteLine($"  <Il2CppType.From(Type) failed: {ex.GetType().Name}: {ex.Message}>");
                        }
                    }

                    // Of<T>()
                    var of = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "Of") return false;
                                if (!m.IsGenericMethodDefinition) return false;
                                if (m.GetGenericArguments().Length != 1) return false;
                                return m.GetParameters().Length == 0;
                            }
                            catch { return false; }
                        });

                    if (of != null)
                    {
                        try
                        {
                            var mg = of.MakeGenericMethod(sysType);
                            var v = mg.Invoke(null, null);
                            if (v != null) return v;
                        }
                        catch (Exception ex)
                        {
                            diag?.WriteLine($"  <Il2CppType.Of<T>() failed for {sysType.FullName}: {ex.GetType().Name}: {ex.Message}>");
                        }
                    }
                }

                // Fallback: look for implicit operator on Il2CppSystem.Type that accepts System.Type
                var il2cppSysType = FindLoadedTypeByFullName("Il2CppSystem.Type");
                if (il2cppSysType != null)
                {
                    var op = il2cppSysType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                        {
                            try
                            {
                                if (m.Name != "op_Implicit") return false;
                                var ps = m.GetParameters();
                                return ps.Length == 1 && ps[0].ParameterType == typeof(Type);
                            }
                            catch { return false; }
                        });

                    if (op != null)
                    {
                        try
                        {
                            var v = op.Invoke(null, new object?[] { sysType });
                            if (v != null) return v;
                        }
                        catch (Exception ex)
                        {
                            diag?.WriteLine($"  <Il2CppSystem.Type op_Implicit(Type) failed: {ex.GetType().Name}: {ex.Message}>");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diag?.WriteLine($"  <TryMakeIl2CppSystemType exception: {ex.GetType().Name}: {ex.Message}>");
            }

            return null;
        }

        private static Type? FindLoadedTypeByFullName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static Type? FindLoadedTypeBySimpleName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t != null && t.Name == simpleName)
                            return t;
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (var t in rtle.Types)
                    {
                        if (t != null && t.Name == simpleName)
                            return t;
                    }
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static void WriteDescendantNameMatches(
            TextWriter w,
            GameObject root,
            string[] needles,
            int maxMatches,
            int maxDepth
        )
        {
            if (!IsAlive(root))
            {
                w.WriteLine("  <root not alive>");
                return;
            }

            if (needles.Length == 0)
            {
                w.WriteLine("  <no needles>");
                return;
            }

            var lowerNeedles = needles
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.ToLowerInvariant())
                .Distinct()
                .ToArray();

            int found = 0;

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                void Recurse(Transform t, string path, int depth)
                {
                    if (found >= maxMatches) return;
                    if (depth > maxDepth) return;

                    string name;
                    try { name = t.gameObject.name ?? "<null-name>"; }
                    catch { name = "<name-error>"; }

                    var nameLower = name.ToLowerInvariant();

                    bool hit = false;
                    foreach (var n in lowerNeedles)
                    {
                        if (nameLower.Contains(n))
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                    {
                        w.WriteLine($"  - {path}/{name}");
                        found++;
                        if (found >= maxMatches) return;
                    }

                    int childCount = 0;
                    try { childCount = t.childCount; }
                    catch { /* ignore */ }

                    for (int i = 0; i < childCount; i++)
                    {
                        Transform child;
                        try { child = t.GetChild(i); }
                        catch { continue; }

                        Recurse(child, $"{path}/{name}", depth + 1);
                        if (found >= maxMatches) return;
                    }
                }

                Recurse(root.transform, "", 0);

                if (found == 0)
                    w.WriteLine("  (none)");
                else if (found >= maxMatches)
                    w.WriteLine($"  <truncated at {maxMatches} matches>");
            }
            catch (Exception ex)
            {
                w.WriteLine($"  <WriteDescendantNameMatches exception {ex.GetType().Name}: {ex.Message}>");
            }
        }


        static Component[] CoerceComponentArray(object ret)
        {
            if (ret == null)
                return Array.Empty<Component>();

            if (ret is Component[] arr)
                return arr;

            // Many IL2CPP bridges return Il2CppReferenceArray<Component> (or similar) which implements IEnumerable.
            if (ret is System.Collections.IEnumerable en)
            {
                var list = new List<Component>();
                foreach (var o in en)
                {
                    if (o is Component c && IsAlive(c))
                        list.Add(c);
                }
                return list.ToArray();
            }

            // Last resort: reflect Length/Count and an indexer.
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var t = ret.GetType();
                var lenProp = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)
                             ?? t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                int len = 0;
                if (lenProp != null)
                {
                    var lv = lenProp.GetValue(ret);
                    if (lv is int li)
                        len = li;
                }

                if (len > 0 && len <= 2048)
                {
                    var getter = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null)
                              ?? t.GetMethod("Item", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);

                    if (getter != null)
                    {
                        var list = new List<Component>(len);
                        for (int i = 0; i < len; i++)
                        {
                            var o = getter.Invoke(ret, new object[] { i });
                            if (o is Component c && IsAlive(c))
                                list.Add(c);
                        }
                        return list.ToArray();
                    }
                }
            }
            catch
            {
                // ignore
            }

            return Array.Empty<Component>();
        }


        // --------------------------
        // Game-owned UI root (preferred): Il2Cpp.fclUI.GetRootGameObject()
        // --------------------------

        private static GameObject? _lastUiRoot;
        private static int _lastUiRootFrame;
        private static string _lastUiRootSource = "";
        private static int GetFrameCountSafe()
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                // Some Unity reference stubs omit Time.frameCount; use reflection when available.
                var p = typeof(Time).GetProperty("frameCount", BindingFlags.Public | BindingFlags.Static);
                if (p != null)
                {
                    var v = p.GetValue(null, null);
                    if (v is int i) return i;
                }

                var m = typeof(Time).GetMethod("get_frameCount", BindingFlags.Public | BindingFlags.Static);
                if (m != null)
                {
                    var v = m.Invoke(null, null);
                    if (v is int i) return i;
                }
            }
            catch { }

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                return (int)(Time.realtimeSinceStartup * 60f);
            }
            catch { }

            return Environment.TickCount;
        }


        private static GameObject? TryGetUiRootViaFclUi(TextWriter w)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var t = FindTypeInLoadedAssemblies("Il2Cpp.fclUI");
                if (t == null)
                {
                    w.WriteLine("fclUI: type Il2Cpp.fclUI not found in loaded assemblies.");
                    return null;
                }

                var mi = t.GetMethod("GetRootGameObject", BindingFlags.Public | BindingFlags.Static);
                if (mi == null)
                {
                    w.WriteLine("fclUI: GetRootGameObject method not found.");
                    return null;
                }

                object? res = mi.Invoke(null, null);

                GameObject? go = res as GameObject;

                if (go == null && res is Component comp)
                    go = comp.gameObject;

                if (go == null && res != null)
                {
                    // Last-resort: look for a `gameObject` property.
                    var p = res.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                    if (p != null)
                    {
                        var pv = p.GetValue(res);
                        if (pv is GameObject pgo)
                            go = pgo;
                    }
                }

                if (!IsAlive(go))
                {
                    w.WriteLine("fclUI: GetRootGameObject returned null (or destroyed).");
                    return null;
                }

                _lastUiRoot = go;
                _lastUiRootFrame = GetFrameCountSafe();
                _lastUiRootSource = "fclUI";

                w.WriteLine($"fclUI root: name={SafeName(go)} id={SafeInstanceId(go)} activeSelf={go!.activeSelf} activeHier={go.activeInHierarchy}");
                return go;
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException;
                if (inner != null)
                    w.WriteLine($"fclUI: invoke failed: {inner.GetType().Name}: {inner.Message}");
                else
                    w.WriteLine($"fclUI: invoke failed: {tie.GetType().Name}: {tie.Message}");
                return null;
            }
            catch (Exception ex)
            {
                w.WriteLine($"fclUI: failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static GameObject? TryGetUiRootCached(TextWriter w)
        {
            if (IsAlive(_lastUiRoot))
            {
                w.WriteLine($"Cached UI root: source={_lastUiRootSource} ageFrames={(GetFrameCountSafe() - _lastUiRootFrame)} name={SafeName(_lastUiRoot)}");
                return _lastUiRoot;
            }
            return null;
        }


        // --------------------------
        // JSONL logging helpers
        // --------------------------

        private void LogJson(string category, string ev, object data)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                if (_jsonl == null) return;

                var sb = new StringBuilder(256);
                sb.Append('{');

                AppendJsonKV(sb, "schema_version", SchemaVersion);
                sb.Append(',');
                AppendJsonKV(sb, "ts", DateTime.Now.ToString("O"));
                sb.Append(',');
                AppendJsonKV(sb, "category", category);
                sb.Append(',');
                AppendJsonKV(sb, "event", ev);
                sb.Append(',');
                sb.Append("\"data\":");
                sb.Append(SimpleJson(data));

                sb.Append('}');
                _jsonl.WriteLine(sb.ToString());
            }
            catch
            {
                // Never let logging crash the mod.
            }
        }

        private static void AppendJsonKV(StringBuilder sb, string k, object? v)
        {
            sb.Append('\"').Append(EscapeJson(k)).Append("\":");
            sb.Append(SimpleJson(v));
        }

        private static string SimpleJson(object? v)
        {
            if (v == null) return "null";

            if (v is string s)
                return "\"" + EscapeJson(s) + "\"";

            if (v is bool b)
                return b ? "true" : "false";

            if (v is int or long or float or double or decimal)
                return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "0";

            if (v is IDictionary<string, object?> dict)
            {
                var sb = new StringBuilder();
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('\"').Append(EscapeJson(kv.Key)).Append("\":");
                    sb.Append(SimpleJson(kv.Value));
                }
                sb.Append('}');
                return sb.ToString();
            }

            // Best-effort: reflect public properties/fields for anonymous types
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var t = v.GetType();
                if (!t.IsPrimitive && t != typeof(string))
                {
                    var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    var sb = new StringBuilder();
                    sb.Append('{');
                    bool first = true;
                    foreach (var p in props)
                    {
                        if (!p.CanRead) continue;
                        object? pv;
                        try { pv = p.GetValue(v, null); } catch { continue; }
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('\"').Append(EscapeJson(p.Name)).Append("\":");
                        sb.Append(SimpleJson(pv));
                    }
                    sb.Append('}');
                    return sb.ToString();
                }
            }
            catch { /* ignore */ }

            return "\"" + EscapeJson(v.ToString() ?? "<null>") + "\"";
        }

        
        // Root-object enumeration that avoids Scene.GetRootGameObjects (often stripped in SMT3HD).
        // We derive roots by scanning all loaded GameObjects and selecting those in the target scene whose Transform has no parent.
        private static List<GameObject> CollectRootObjectsBySceneScan(Scene scene, TextWriter w, int maxRoots)
{
    var roots = new List<GameObject>();

    // Note: SMT3HD IL2CPP can strip or partially stub Unity enumeration APIs.
    // Strategy:
    //   (1) game-owned UI root via fclUI.GetRootGameObject()
    //   (2) Scene roots via Scene.GetRootGameObjects(List*) (preferred), then Scene.GetRootGameObjects()
    //   (3) ACTIVE-only transform scan via Object.FindObjectsOfType<Transform>()
    //   (4) Anchor scan via GameObject.Find(...) to avoid total-blackout cases

    try
    {
        w.WriteLine("Root enumeration strategy:");
        w.WriteLine("- Preferred: game-owned UI root via Il2Cpp.fclUI.GetRootGameObject() (reflection).");
        w.WriteLine("- Fallback: Scene.GetRootGameObjects(List*) (preferred) then Scene.GetRootGameObjects() (array).");
        w.WriteLine("- Last resort: Object.FindObjectsOfType<Transform>() (active-only), then anchor GameObject.Find(..).");
        w.WriteLine();

        // 1) fclUI root (best for shops/terminal + certain menus)
        var uiRoot = TryGetUiRootViaFclUi(w);
        if (!IsAlive(uiRoot))
            uiRoot = TryGetUiRootCached(w);

        if (IsAlive(uiRoot))
        {
            roots.Add(uiRoot!);
            bool hier = false;
            try { hier = uiRoot!.activeInHierarchy; } catch { hier = false; }

            if (hier)
                w.WriteLine($"Added fclUI root (activeInHierarchy=True): {SafeName(uiRoot)}");
            else
                w.WriteLine($"Added fclUI root but it is not active in hierarchy (may be stale / transitional): {SafeName(uiRoot)}");

            w.WriteLine("Continuing with Scene roots as well (to keep deltas comparable across UI contexts)…");
            w.WriteLine();
        }
        else
        {
            w.WriteLine("No UI root available from fclUI (and no cached root). Falling back to Scene roots…");
            w.WriteLine();
        }

        // 2) Scene roots via reflection.
        // IMPORTANT: On SMT3HD IL2CPP + Il2CppInterop, the array overload Scene.GetRootGameObjects() may throw
        // MissingMethodException due to Il2CppSystem.Collections.Generic.List<T>.ToArray() being stubbed.
        // Prefer the 1-arg List overload if present; we can fill and index without calling ToArray().
        bool gotAny = false;

        try
        {
            var st = typeof(Scene);
            object boxedScene = scene; // Scene is a struct

            // 2a) Prefer: void GetRootGameObjects(<some List<GameObject>>)
            MethodInfo? mList = null;
            Type? listParamType = null;

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                foreach (var mm in st.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!string.Equals(mm.Name, "GetRootGameObjects", StringComparison.Ordinal))
                        continue;

                    var ps = mm.GetParameters();
                    if (ps.Length != 1)
                        continue;

                    var pt = ps[0].ParameterType;
                    if (pt == null || !pt.IsGenericType)
                        continue;

                    var gtd = pt.GetGenericTypeDefinition();
                    if (gtd == null || !string.Equals(gtd.Name, "List`1", StringComparison.Ordinal))
                        continue;

                    var ga = pt.GetGenericArguments();
                    if (ga.Length != 1 || ga[0] != typeof(GameObject))
                        continue;

                    mList = mm;
                    listParamType = pt;
                    break;
                }
            }
            catch (Exception ex)
            {
                w.WriteLine($"Scene.GetRootGameObjects(List*) overload scan failed: {ex.GetType().Name}: {ex.Message}");
            }

            if (mList != null && listParamType != null)
            {
                object? listObj = null;
                try
                {
                    listObj = Activator.CreateInstance(listParamType);
                }
                catch (Exception ex)
                {
                    w.WriteLine($"Scene.GetRootGameObjects(List*) list create failed ({listParamType.FullName}): {ex.GetType().Name}: {ex.Message}");
                }

                if (listObj != null)
                {
                    try
                    {
                        mList.Invoke(boxedScene, new object?[] { listObj });

                        int count = 0;
                        try
                        {
                            var pCount = listParamType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                            if (pCount != null)
                                count = Convert.ToInt32(pCount.GetValue(listObj, null));
                            else
                            {
                                var mCount = listParamType.GetMethod("get_Count", BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);
                                if (mCount != null)
                                    count = Convert.ToInt32(mCount.Invoke(listObj, null));
                            }
                        }
                        catch
                        {
                            count = 0;
                        }

                        MethodInfo? mGetItem = listParamType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new[] { typeof(int) }, modifiers: null);

                        int got = 0;
                        for (int i = 0; i < count; i++)
                        {
                            object? item = null;
                            try { item = (mGetItem != null) ? mGetItem.Invoke(listObj, new object?[] { i }) : null; }
                            catch { item = null; }

                            if (item is GameObject go && IsAlive(go))
                            {
                                if (!roots.Contains(go))
                                    roots.Add(go);

                                got++;
                                if (maxRoots > 0 && roots.Count >= maxRoots)
                                    break;
                            }
                        }

                        gotAny = roots.Count > 0;
                        w.WriteLine($"Scene.GetRootGameObjects(List*) => {got} (kept {roots.Count}, cap {maxRoots})");
                    }
                    catch (TargetInvocationException tie)
                    {
                        var inner = tie.InnerException;
                        if (inner != null)
                            w.WriteLine($"Scene.GetRootGameObjects(List*) invocation failed: {inner.GetType().Name}: {inner.Message}");
                        else
                            w.WriteLine($"Scene.GetRootGameObjects(List*) invocation failed: {tie.GetType().Name}: {tie.Message}");
                    }
                    catch (Exception ex)
                    {
                        w.WriteLine($"Scene.GetRootGameObjects(List*) failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            else
            {
                w.WriteLine("Scene.GetRootGameObjects(List*) overload: MISSING");
            }

            // 2b) Fallback: GameObject[] GetRootGameObjects()
            if (!gotAny)
            {
                try
                {
                    var mArr = st.GetMethod(
                        "GetRootGameObjects",
                        BindingFlags.Public | BindingFlags.Instance,
                        binder: null,
                        types: Type.EmptyTypes,
                        modifiers: null);

                    if (mArr == null)
                    {
                        w.WriteLine("Scene.GetRootGameObjects() overload: MISSING");
                    }
                    else
                    {
                        object? res = null;

                        try { res = mArr.Invoke(boxedScene, null); }
                        catch (TargetInvocationException tie)
                        {
                            var inner = tie.InnerException;
                            if (inner != null)
                                w.WriteLine($"Scene.GetRootGameObjects() invocation failed: {inner.GetType().Name}: {inner.Message}");
                            else
                                w.WriteLine($"Scene.GetRootGameObjects() invocation failed: {tie.GetType().Name}: {tie.Message}");
                            res = null;
                        }

                        int got = 0;
                        if (res != null)
                        {
                            foreach (var item in EnumerateUnknownEnumerable(res))
                            {
                                if (item is GameObject go && IsAlive(go))
                                {
                                    if (!roots.Contains(go))
                                        roots.Add(go);

                                    got++;
                                    if (maxRoots > 0 && roots.Count >= maxRoots)
                                        break;
                                }
                            }
                        }

                        gotAny = roots.Count > 0;
                        w.WriteLine($"Scene.GetRootGameObjects() => {got} (kept {roots.Count}, cap {maxRoots})");
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"Scene.GetRootGameObjects() failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            w.WriteLine($"Scene root enumeration failed: {ex.GetType().Name}: {ex.Message}");
        }

        // 3) Last resort: derive roots from ACTIVE transforms (works even when Resources.FindObjectsOfTypeAll is missing).
        // This will NOT see inactive UI trees, but it’s much better than failing the entire dump.
        bool gotAnyAfterScene = roots.Count > 0;

        if (!gotAnyAfterScene)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                int totalTransforms = 0;
                int keptRoots = 0;

                Transform[]? all = null;
                try
                {
                    // Use reflection to avoid compile-time dependency on UnityEngine.Object.FindObjectsOfType<T>()
                    // (some UnityEngine reference assemblies in mod toolchains omit this API).
                    MethodInfo? mGen0 = null;
                    MethodInfo? mGen1 = null;
                    try
                    {
                        foreach (var mi in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (!string.Equals(mi.Name, "FindObjectsOfType", StringComparison.Ordinal))
                                continue;
                            if (!mi.IsGenericMethodDefinition)
                                continue;
                            var ps = mi.GetParameters();
                            if (ps.Length == 0)
                            {
                                mGen0 = mi;
                                break;
                            }
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(bool))
                                mGen1 = mi;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    object? res = null;
                    if (mGen0 != null)
                    {
                        try { res = mGen0.MakeGenericMethod(typeof(Transform)).Invoke(null, null); } catch { res = null; }
                    }
                    else if (mGen1 != null)
                    {
                        // bool param is commonly "includeInactive". We want active-only, so pass false.
                        try { res = mGen1.MakeGenericMethod(typeof(Transform)).Invoke(null, new object?[] { false }); } catch { res = null; }
                    }
                    else
                    {
                        w.WriteLine("Object.FindObjectsOfType<T>() generic overload: MISSING (reflection)");
                    }

                    if (res is Transform[] direct)
                    {
                        all = direct;
                    }
                    else if (res != null)
                    {
                        var tmp = new List<Transform>();
                        foreach (var item in EnumerateUnknownEnumerable(res))
                            if (item is Transform t && IsAlive(t))
                                tmp.Add(t);
                        if (tmp.Count > 0)
                            all = tmp.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"Object.FindObjectsOfType<Transform>() failed (reflection path): {ex.GetType().Name}: {ex.Message}");
                    all = null;
                }

                if (all != null)
                {
                    foreach (var t in all)
                    {
                        totalTransforms++;
                        if (t == null)
                            continue;

                        GameObject? go = null;
                        try { go = t.gameObject; } catch { go = null; }

                        if (!IsAlive(go))
                            continue;

                        bool isRoot = true;
                        try { isRoot = (t.parent == null); } catch { isRoot = true; }

                        if (!isRoot)
                            continue;

                        if (!roots.Contains(go!))
                        {
                            roots.Add(go!);
                            keptRoots++;
                            if (maxRoots > 0 && roots.Count >= maxRoots)
                                break;
                        }
                    }
                }

                w.WriteLine($"TransformScan: totalTransforms={totalTransforms}, rootsAdded={keptRoots}, roots={roots.Count} (cap {maxRoots})");
            }
            catch (Exception ex)
            {
                w.WriteLine($"TransformScan failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // 4) Anchor scan: if enumeration APIs are stubbed, try finding a few well-known objects by name.
        if (roots.Count == 0)
        {
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                string[] anchors = new[]
                {
                    "Main Camera",
                    "fclUI",
                    "EventSystem",
                    "UICamera",
                    "UI",
                    "Hud",
                    "HUD"
                };

                int found = 0;
                foreach (var a in anchors)
                {
                    GameObject? go = null;
                    try { go = GameObject.Find(a); } catch { go = null; }
                    if (!IsAlive(go))
                        continue;

                    // climb to top
                    Transform? t = null;
                    try { t = go!.transform; } catch { t = null; }
                    Transform? top = t;
                    while (top != null)
                    {
                        Transform? p = null;
                        try { p = top.parent; } catch { p = null; }
                        if (p == null) break;
                        top = p;
                    }

                    GameObject? rootGo = null;
                    try { rootGo = top != null ? top.gameObject : go; } catch { rootGo = go; }

                    if (IsAlive(rootGo) && !roots.Contains(rootGo!))
                    {
                        roots.Add(rootGo!);
                        found++;
                        if (maxRoots > 0 && roots.Count >= maxRoots)
                            break;
                    }
                }

                w.WriteLine($"AnchorScan: rootsAdded={found}, roots={roots.Count} (cap {maxRoots})");
            }
            catch (Exception ex)
            {
                w.WriteLine($"AnchorScan failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // If we ended up with a lot of roots, prioritize UI-ish root names to keep probe output sane.
        if (roots.Count > 1)
        {
            bool LooksUi(string? n)
            {
                if (string.IsNullOrEmpty(n)) return false;
                var s = n.ToLowerInvariant();
                return s.Contains("ui") || s.Contains("hud") || s.Contains("menu") || s.Contains("msg") || s.Contains("dialog") ||
                       s.Contains("dlg") || s.Contains("talk") || s.Contains("subtitle") || s.Contains("text") || s.Contains("window") ||
                       s.Contains("panel") || s.Contains("canvas") || s.Contains("system");
            }

            var ui = roots.Where(r => r != null && LooksUi(r.name)).ToList();
            if (ui.Count > 0)
            {
                w.WriteLine($"Filtered roots by UI-ish name: {roots.Count} -> {ui.Count}");
                roots = ui;
            }
        }
    }
    catch (Exception ex)
    {
        w.WriteLine($"CollectRootObjects failed: {ex.GetType().Name}: {ex.Message}");
    }

    roots.Sort((a, b) => string.Compare(a != null ? a.name : "", b != null ? b.name : "", StringComparison.Ordinal));

    // Hard cap safety (even after filtering) to avoid exploding file size.
    const int hardCap = 32;
    int cap = maxRoots > 0 ? Math.Min(maxRoots, hardCap) : hardCap;
    if (roots.Count > cap)
    {
        w.WriteLine($"Truncating roots: {roots.Count} -> {cap} (hard cap)");
        roots = roots.Take(cap).ToList();
    }

    return roots;
}

/// <summary>
    /// Enumerate arrays / Il2Cpp arrays / unknown IEnumerable results safely without hard-binding to a concrete collection type.
    /// </summary>
    private static List<object?> EnumerateUnknownEnumerable(object? res)
    {
        var items = new List<object?>();
        if (res == null)
            return items;

        // Avoid treating strings as IEnumerable<char>.
        if (res is string)
        {
            items.Add(res);
            return items;
        }

        // Standard managed IEnumerable
        if (res is System.Collections.IEnumerable en)
        {
            foreach (var item in en)
                items.Add(item);
            return items;
        }

        // IL2CPP / interop "array-like" objects sometimes expose Length/Count + get_Item(int)
        // but do NOT implement managed IEnumerable.
        try
        {
            var t = res.GetType();

            var lenProp = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)
                       ?? t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);

            if (lenProp != null)
            {
                int len = 0;
                try
                {
                    var v = lenProp.GetValue(res, index: null);
                    if (v != null)
                        len = Convert.ToInt32(v);
                }
                catch
                {
                    len = 0;
                }

                if (len > 0)
                {
                    MethodInfo? getItem = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new[] { typeof(int) }, modifiers: null);

                    if (getItem == null)
                    {
                        var idxProp = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, binder: null, returnType: null, types: new[] { typeof(int) }, modifiers: null);
                        if (idxProp != null)
                            getItem = idxProp.GetGetMethod(nonPublic: false);
                    }

                    if (getItem != null)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            object? item = null;
                            try { item = getItem.Invoke(res, new object?[] { i }); }
                            catch { item = null; }
                            items.Add(item);
                        }

                        return items;
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        // Non-enumerable single value.
        items.Add(res);
        return items;
    }

    
    // =========================
    // Pass A8: Controller-state probes (F12) + terminal seam snapshot (F3)
    // =========================

    private void DumpUiControllerState()
    {
        string path = MakeDumpPath("ui_controller_state");
        _lastDumpPath = path;
        MelonLogger.Msg($"[Reimagined] UI controller state dump -> {path}");

        try
        {
            using (var w = new StreamWriter(path, append: false, Encoding.UTF8))
            {
                w.WriteLine("=== UI Controller State ===");
                w.WriteLine($"time={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                w.WriteLine();

                // Build a snapshot so we can resolve dynamic paths like saveUI(Clone).
                var scene = SceneManager.GetActiveScene();
                var roots = CollectRootObjectsBySceneScan(scene, w, 256);
                var snap = new Dictionary<string, UiNodeState>(StringComparer.Ordinal);
                int nodeCount = 0;
                foreach (var r in roots)
                {
                    if (!IsAlive(r)) continue;
                    SnapshotObjectRecursive(r, SafeName(r), snap, ref nodeCount, maxNodes: 8000);
                    if (nodeCount >= 8000) break;
                }

                // Resolve key anchors (best-effort).
                string? campKey = FindFirstKeyBySuffix(snap, "Canvas_UI/campUIBase/campUI") ?? FindFirstKeyByLeafPrefix(snap, "campUI");
                string? shopKey = FindFirstKeyBySuffix(snap, "Canvas_UI/shopUI") ?? FindFirstKeyByLeafPrefix(snap, "shopUI");
                string? termKey = FindFirstKeyBySuffix(snap, "Canvas_UI/terminalUI") ?? FindFirstKeyByLeafPrefix(snap, "terminalUI");
                string? saveKey = FindFirstKeyBySuffix(snap, "Canvas_UI/saveUI(Clone)") ?? FindFirstKeyByLeafPrefix(snap, "saveUI(Clone)") ?? FindFirstKeyByLeafPrefix(snap, "saveUI");

                DumpControllerAnchorSection(w, roots, campKey, label: "Overworld/Pause: campUI");
                DumpControllerAnchorSection(w, roots, shopKey, label: "Shop: shopUI");
                DumpControllerAnchorSection(w, roots, termKey, label: "Terminal: terminalUI");
                DumpControllerAnchorSection(w, roots, saveKey, label: "Save Screen: saveUI(Clone)");

                w.WriteLine();
                w.WriteLine("=== Notes ===");
                w.WriteLine("- This dump is intentionally conservative: it only logs a small, stable surface (Text fields, cursor actives, and primitive fields).");
                w.WriteLine("- If a field read throws (Il2Cpp constraint/type quirks), the dumper will skip it and continue.");
            }

            OverlayLog("dump", $"ui_controller_state: {Path.GetFileName(path)}");
            Toast("UI controller state written", ToastKind.Ok);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Reimagined] DumpUiControllerState failed: {ex}");
            Toast("UI controller state failed", ToastKind.Error);
        }
    }

    private void DumpControllerAnchorSection(StreamWriter w, List<GameObject> roots, string? anchorKey, string label)
    {
        w.WriteLine($"== {label} ==");
        if (string.IsNullOrEmpty(anchorKey))
        {
            w.WriteLine("  <anchor not found in snapshot>");
            w.WriteLine();
            return;
        }

        var goAnchor = TryFindGameObjectBySnapshotPath(roots, anchorKey);
        if (goAnchor == null)
        {
            w.WriteLine($"  <anchor resolved key='{anchorKey}' but GameObject not found>");
            w.WriteLine();
            return;
        }

        w.WriteLine($"  key={anchorKey}");
        w.WriteLine($"  go={SafeName(goAnchor)} activeInHierarchy={SafeBool(() => goAnchor.activeInHierarchy)}");

        // Controller candidates we care about right now:
        // - saveUI / saveFileUI (save screen)
        // - campMenu (facility menus)
        // We locate them by (assembly,name) rather than generic GetComponent<T>() to avoid Unity reference-surface issues.
        var matches = new List<(string goPath, Component comp, string displayType)>(64);

        TraverseGameObjectTree(goAnchor, maxNodes: 7000, (go, goPath) =>
        {
            var comps = GetComponentsSafe(go);
            if (comps.Length == 0)
                return;

            foreach (var c in comps)
            {
                string managedType;
                string? refHint;
                var display = FormatComponentTypeForDump(c, out refHint, out managedType);

                // Match on Assembly-CSharp types we care about.
                // display is e.g. "Assembly-CSharp::saveUI"
                if (display.EndsWith("::saveUI") || display.EndsWith("::saveFileUI") || display.EndsWith("::campMenu"))
                {
                    matches.Add((goPath, c, display));
                }
            }
        });

        // Summarize what we found.
        if (matches.Count == 0)
        {
            w.WriteLine("  <no controller candidates found under this anchor>");
            w.WriteLine();
            return;
        }

        // saveUI first (special formatter)
        foreach (var m in matches)
        {
            if (m.displayType.EndsWith("::saveUI"))
            {
                DumpSaveUiController(w, m.goPath, m.comp);
                break;
            }
        }

        // saveFileUI (summarize a handful + selected cursor)
        int saveFileCount = 0;
        int saveFileCursorIdx = -1;
        for (int i = 0; i < matches.Count; i++)
        {
            if (!matches[i].displayType.EndsWith("::saveFileUI"))
                continue;

            saveFileCount++;

            // For the first 12, print a compact line. (There can be ~20.)
            if (saveFileCount <= 12)
            {
                var line = DescribeSaveFileUi(matches[i].comp, slotIndex: saveFileCount - 1, out bool cursorActive);
                w.WriteLine($"  {matches[i].displayType} @ {matches[i].goPath}");
                w.WriteLine($"    {line}");
                if (cursorActive && saveFileCursorIdx < 0)
                    saveFileCursorIdx = saveFileCount - 1;
            }
        }
        if (saveFileCount > 0)
        {
            w.WriteLine($"  saveFileUI_count={saveFileCount} selected_cursor_index={(saveFileCursorIdx >= 0 ? saveFileCursorIdx.ToString() : "<none>")}");
        }

        // campMenu (print a small primitive-field surface for a few instances)
        int campMenusDumped = 0;
        foreach (var m in matches)
        {
            if (!m.displayType.EndsWith("::campMenu"))
                continue;

            if (campMenusDumped >= 4)
                break;

            campMenusDumped++;
            w.WriteLine($"  {m.displayType} @ {m.goPath}");
            DumpPrimitiveFieldsBestEffort(w, m.comp, maxFields: 50, indent: "    ");
        }

        if (campMenusDumped == 0)
            w.WriteLine("  campMenu: <none found>");

        w.WriteLine();
    }


    private static string DescribeComponent(Component comp)
    {
        if (comp == null)
            return "<null component>";

        string typeName;
        try
        {
            typeName = TryGetIl2CppNativeFullName(comp) ?? (comp.GetType().FullName ?? comp.GetType().Name);
        }
        catch
        {
            typeName = "<unknown-type>";
        }

        string goName;
        try { goName = SafeName(comp.gameObject); }
        catch { goName = "<no-gameobject>"; }

        return $"{typeName} on '{goName}'";
    }

    private void DumpSaveUiController(StreamWriter w, string goPath, Component comp)
{
    // This is the "high-value" state dump for Save UI.
    // Important: Il2CppInterop generates most members as PROPERTIES, so we must use TryGetFieldValue (field-or-prop).

    string F(object? o)
    {
        if (o == null) return "null";
        try { return o.ToString() ?? "<null>"; }
        catch { return "<unreadable>"; }
    }

    w.WriteLine("  saveUI: " + DescribeComponent(comp));

    // Scalar-ish controller fields
    object? isOpenObj = TryGetFieldValue(comp, "_isOpen");
    object? isInitObj = TryGetFieldValue(comp, "_isInit");
    object? isPauseObj = TryGetFieldValue(comp, "_isPause");
    object? isQuickObj = TryGetFieldValue(comp, "_isQuickSave");
    object? notOpenObj = TryGetFieldValue(comp, "_notOpenSave");
    object? selectSlotObj = TryGetFieldValue(comp, "_selectSlot");
    object? forceTermObj = TryGetFieldValue(comp, "_forceTerminalNo");

    w.WriteLine($"    _isOpen={F(isOpenObj)} _isInit={F(isInitObj)} _isPause={F(isPauseObj)} _isQuickSave={F(isQuickObj)} _notOpenSave={F(notOpenObj)} _selectSlot={F(selectSlotObj)} _forceTerminalNo={F(forceTermObj)}");

    // Choice cursor (some flows use a shared cursor rather than per-slot cursor)
    var choiceCursorGo = TryGetFieldValue(comp, "_choiceCursor") as GameObject;
    if (choiceCursorGo != null)
        w.WriteLine($"    _choiceCursor='{SafeName(choiceCursorGo)}' activeInHierarchy={choiceCursorGo.activeInHierarchy}");

    // Save slot list
    object? fileListObj = TryGetFieldValue(comp, "_fileList");
    if (fileListObj == null)
        w.WriteLine("    _fileList=<null/unreadable> (will fallback to child scan)");
    else
        w.WriteLine($"    _fileList={fileListObj.GetType().FullName}");

    // Enumerate slots (prefer _fileList; fallback to scanning children)
    List<object?> items = new List<object?>();

    if (fileListObj != null)
        items.AddRange(TryEnumerateIndexable(fileListObj, maxItems: 32));

    if (items.Count == 0)
    {
        // Fallback scan: find saveFileUI components under this saveUI GameObject.
        try
        {
            TraverseGameObjectTree(comp.gameObject, 2500, (go, goPath) =>
            {
                try
                {
                    foreach (var c in GetComponentsSafe(go))
                    {
                        if (c == null) continue;
                        string? native = TryGetIl2CppNativeFullName(c);
                        if (native != null && native.EndsWith("::saveFileUI", StringComparison.Ordinal))
                        {
                            items.Add(c);
                        }
                    }
                }
                catch { /* ignore per-node */ }
            });
        }
        catch { /* ignore */ }
    }

    w.WriteLine($"    saveFileUI_count={items.Count}");

    int emit = Math.Min(items.Count, 16);
    for (int i = 0; i < emit; i++)
    {
        Component? slotComp = items[i] as Component;
        if (slotComp == null)
        {
            w.WriteLine($"      [{i}] <non-component: {items[i]?.GetType().FullName ?? "null"}>");
            continue;
        }

        w.WriteLine("      " + DescribeSaveFileUi(slotComp, slotIndex: i, maxTextLen: 96));
    }

    if (items.Count > emit)
        w.WriteLine($"      ... ({items.Count - emit} more)");

    // Cursor / selection markers help even if Save UI field access changes.
    DumpCursorMarkers(w, comp.gameObject, maxNodes: 2200, maxLines: 40);
}

    private string DescribeSaveFileUi(Component slotComp, int slotIndex, int maxTextLen = 96)
    {
        return DescribeSaveFileUi(slotComp, slotIndex, out _, maxTextLen);
    }

    private string DescribeSaveFileUi(Component slotComp, int slotIndex, out bool cursorActive, int maxTextLen = 96)
    {
        cursorActive = false;

        string Clip(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            // Keep single-line for logs
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (s.Length <= maxTextLen)
                return s;

            int take = Math.Max(0, maxTextLen - 1);
            return (take > 0 ? s.Substring(0, take) : "") + "…";
        }

        int? slotNo = null;
        object? slotObj = TryGetFieldValue(slotComp, "_slot");
        if (slotObj is int i)
            slotNo = i;

        // Primary (documented) fields
        string? noData = TryGetTmpText(TryGetFieldValue(slotComp, "_noDataText"));
        string? player = TryGetTmpText(TryGetFieldValue(slotComp, "_playerName"));
        string? terminal = TryGetTmpText(TryGetFieldValue(slotComp, "_terminalName"));
        string? diff = TryGetTmpText(TryGetFieldValue(slotComp, "_difficulty"));

        var cursorGo = TryGetFieldValue(slotComp, "_cursor") as GameObject;
        cursorActive = cursorGo != null && SafeBool(() => cursorGo.activeInHierarchy);

        // Fallback: if everything is empty, scrape the first non-empty TMP-like text under this slot.
        if (string.IsNullOrWhiteSpace(noData) &&
            string.IsNullOrWhiteSpace(player) &&
            string.IsNullOrWhiteSpace(terminal) &&
            string.IsNullOrWhiteSpace(diff))
        {
            string? any = TryFindFirstTextInChildren(slotComp, maxItems: 256);
            if (!string.IsNullOrWhiteSpace(any))
                player = any;
        }

        bool active = SafeBool(() => slotComp.gameObject.activeInHierarchy);

        return $"slotIdx={slotIndex} slot={(slotNo.HasValue ? slotNo.Value.ToString() : "?")} active={active} cursor={(cursorActive ? "ON" : "off")} " +
               $"player='{Clip(player)}' terminal='{Clip(terminal)}' diff='{Clip(diff)}' nodata='{Clip(noData)}'";
    }



    private void DumpTerminalSeamState()
    {
        try
        {
            string path = MakeDumpPath("terminal_seam_state");
            _lastDumpPath = path;
            using (var w = new StreamWriter(path, append: false, Encoding.UTF8))
            {
                w.WriteLine($"timestamp={DateTime.Now:O}");
                w.WriteLine($"allow_invokes={_allowTerminalInvokes}");
                w.WriteLine();

                // Terminal seam helpers live across two classes in Assembly-CSharp:
                // - fclTerminalInit : "static state" getters (call mode / event stat / process checks)
                // - fclTerminalUpdate : some update-time state (e.g. save action flag / jump terminal no)
                //
                // IMPORTANT: Some "getter" methods can still have side effects in IL2CPP games.
                // We keep a hard safety gate: enable invokes explicitly in the overlay Settings.

                void SafeLine(string t, string m)
                    => w.WriteLine($"{t}.{m} -> <skipped: invokes disabled (enable in overlay Settings)>");

                if (!_allowTerminalInvokes)
                {
                    SafeLine("Il2Cpp.fclTerminalInit", "trmGetCallMode");
                    SafeLine("Il2Cpp.fclTerminalInit", "trmGetEventStat");
                    SafeLine("Il2Cpp.fclTerminalInit", "fclChkTerminalProcess");
                    SafeLine("Il2Cpp.fclTerminalInit", "trmChkEventWait");
                    SafeLine("Il2Cpp.fclTerminalUpdate", "trmCheckSaveAct");
                    SafeLine("Il2Cpp.fclTerminalUpdate", "trmGetJumpTerminalNo");
                }
                else
                {
                    DumpStaticGetter(w, "Il2Cpp.fclTerminalInit", "trmGetCallMode");
                    DumpStaticGetter(w, "Il2Cpp.fclTerminalInit", "trmGetEventStat");
                    DumpStaticGetter(w, "Il2Cpp.fclTerminalInit", "fclChkTerminalProcess");
                    DumpStaticGetter(w, "Il2Cpp.fclTerminalInit", "trmChkEventWait");

                    DumpStaticGetter(w, "Il2Cpp.fclTerminalUpdate", "trmCheckSaveAct");
                    DumpStaticGetter(w, "Il2Cpp.fclTerminalUpdate", "trmGetJumpTerminalNo");
                }

                w.WriteLine();
                GameDebugMenuBridge.WriteCurrentTerminalStaticWorkProbe(w);

                w.WriteLine();
                w.WriteLine("[selected route-favorite candidate]");
                GameDebugMenuBridge.WriteSelectedWarpCatalogRouteFavoriteTerminalSeamProbe(w);
            }

            MelonLogger.Msg($"[Reimagined] wrote terminal seam state dump: {path}");
            OverlayLog("dump", $"terminal_seam_state: {Path.GetFileName(path)}");
            Toast("Terminal seam state written", ToastKind.Ok);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Reimagined] DumpTerminalSeamState failed: {ex}");
            Toast("Terminal seam dump failed", ToastKind.Error);
        }
    }

    private void DumpStaticGetter(StreamWriter w, string typeFullName, string methodName)
    {
        var t = TryFindLoadedType(typeFullName);
        if (t == null)
        {
            w.WriteLine($"{typeFullName}.{methodName} -> <type not found>");
            return;
        }

        MethodInfo? mi = null;
        try
        {
            mi = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }
        catch (Exception ex)
        {
            w.WriteLine($"{typeFullName}.{methodName} -> <GetMethod failed: {ex.GetType().Name}: {ex.Message}>");
            return;
        }

        if (mi == null)
        {
            w.WriteLine($"{typeFullName}.{methodName} -> <method not found>");
            return;
        }

        try
        {
            var ps = mi.GetParameters();
            if (ps != null && ps.Length != 0)
            {
                w.WriteLine($"{typeFullName}.{methodName} -> <skipped: expects {ps.Length} params>");
                return;
            }
        }
        catch
        {
            // If parameter inspection fails, still attempt invoke (best-effort).
        }

        try
        {
            var res = mi.Invoke(null, parameters: null);
            w.WriteLine($"{typeFullName}.{methodName} -> {FormatValueCompact(res)}");
        }
        catch (TargetInvocationException tie)
        {
            var inner = tie.InnerException;
            w.WriteLine($"{typeFullName}.{methodName} -> <invoke threw: {(inner != null ? inner.GetType().Name : tie.GetType().Name)}: {(inner != null ? inner.Message : tie.Message)}>");
        }
        catch (Exception ex)
        {
            w.WriteLine($"{typeFullName}.{methodName} -> <invoke failed: {ex.GetType().Name}: {ex.Message}>");
        }
    }

    
    private static string? FindFirstKeyBySuffix(Dictionary<string, UiNodeState> snap, string suffix)
    {
        if (snap == null || string.IsNullOrEmpty(suffix))
            return null;

        try
        {
            foreach (var k in snap.Keys)
            {
                if (k != null && k.EndsWith(suffix, StringComparison.Ordinal))
                    return k;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

private static Type? TryFindLoadedType(string fullName)
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (t != null)
                        return t;
                }
                catch
                {
                    // ignore and continue
                }
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

        private static object? TryGetFieldValue(object obj, string fieldName)
    {
        if (obj == null || string.IsNullOrEmpty(fieldName))
            return null;

        Type t = obj.GetType();

        // 1) Field lookup (rare with Il2CppInterop, but keep it)
        try
        {
            var fi = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
                return fi.GetValue(obj);
        }
        catch
        {
            // ignore
        }

        // 2) Property lookup (common with Il2CppInterop)
        try
        {
            var pi = t.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi != null)
                return pi.GetValue(obj, null);
        }
        catch
        {
            // ignore
        }

        // 3) Common naming variant: strip leading underscores
        if (fieldName.Length > 1 && fieldName[0] == '_')
        {
            string alt = fieldName.TrimStart('_');
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var pi2 = t.GetProperty(alt, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pi2 != null)
                    return pi2.GetValue(obj, null);
            }
            catch
            {
                // ignore
            }

            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var fi2 = t.GetField(alt, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi2 != null)
                    return fi2.GetValue(obj);
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }



    private static string? TryFindFirstTextInChildren(Component root, int maxItems)
    {
        if (root == null || maxItems <= 0)
            return null;

        try
        {
            // Use the non-generic overload to avoid Il2CppInterop generic-shim weirdness.
            GameObject? rootGo = null;
            try { rootGo = root.gameObject; } catch { rootGo = null; }
            if (rootGo == null)
                return null;

            int seen = 0;
            string? found = null;
            TraverseGameObjectTree(rootGo, 1500, (go, goPath) =>
            {
                if (found != null) return;
                if (seen >= maxItems) return;
                Component[] compsHere;
                try { compsHere = GetComponentsSafe(go); } catch { compsHere = Array.Empty<Component>(); }
                foreach (var c in compsHere)
                {
                    if (seen++ >= maxItems) return;
                    if (c == null) continue;
                    string? s = TryGetTextPropertyStrict(c);
                    if (!string.IsNullOrWhiteSpace(s)) { found = s; return; }
                }
            });
            if (!string.IsNullOrWhiteSpace(found))
                return found;
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? TryGetTextPropertyStrict(object? obj)
    {
        if (obj == null)
            return null;

        try
        {
            var t = obj.GetType();
            var pi = t.GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi == null)
                return null;

            object? v = null;
            try { v = pi.GetValue(obj, null); } catch { v = null; }

            string? s = v as string ?? v?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                return null;

            return s;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetTmpText(object? tmp)
    {
        if (tmp == null)
            return null;

        try
        {
            var t = tmp.GetType();
            var pi = t.GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi == null)
                return tmp.ToString();

            var v = pi.GetValue(tmp, index: null);
            return v != null ? v.ToString() : null;
        }
        catch
        {
            try { return tmp.ToString(); } catch { return null; }
        }
    }

    private static string FormatValueCompact(object? v)
    {
        if (v == null)
            return "<null>";

        try
        {
            // UnityEngine.Object has a name + null-behavior; keep it simple.
            var ueObj = v as UnityEngine.Object;
            if (ueObj != null)
            {
                string n = "";
                try { n = ueObj.name; } catch { n = ""; }
                return $"{v.GetType().FullName}('{n}')";
            }
        }
        catch
        {
            // ignore
        }

        try { return v.ToString() ?? "<null>"; }
        catch { return "<toString-failed>"; }
    }

    private static void DumpPrimitiveFieldsBestEffort(StreamWriter w, Component comp, int maxFields, string indent)
    {
        try
        {
            var t = comp.GetType();
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int printed = 0;

            foreach (var fi in fields)
            {
                if (printed >= maxFields)
                    break;

                object? val = null;
                try { val = fi.GetValue(comp); }
                catch { continue; }

                if (val == null)
                    continue;

                // Prefer printing primitive-ish values and obvious Unity handles.
                if (val is string || val is bool || val is byte || val is sbyte || val is short || val is ushort ||
                    val is int || val is uint || val is long || val is ulong || val is float || val is double || val is decimal)
                {
                    w.WriteLine($"{indent}{fi.Name} = {val}");
                    printed++;
                    continue;
                }

                var enumType = val.GetType();
                if (enumType.IsEnum)
                {
                    w.WriteLine($"{indent}{fi.Name} = {val} ({enumType.FullName})");
                    printed++;
                    continue;
                }

                var go = val as GameObject;
                if (go != null)
                {
                    w.WriteLine($"{indent}{fi.Name} = GameObject('{SafeName(go)}' activeInHierarchy={SafeBool(() => go.activeInHierarchy)})");
                    printed++;
                    continue;
                }

                var c = val as Component;
                if (c != null)
                {
                    w.WriteLine($"{indent}{fi.Name} = Component({c.GetType().FullName} on '{SafeName(c.gameObject)}')");
                    printed++;
                    continue;
                }

                // TMPro text fields (best-effort via property)
                var maybeText = TryGetTmpText(val);
                if (!string.IsNullOrEmpty(maybeText) && maybeText.Length <= 80)
                {
                    w.WriteLine($"{indent}{fi.Name} = {maybeText}");
                    printed++;
                    continue;
                }
            }

            if (printed == 0)
                w.WriteLine($"{indent}<no printable primitive fields found>");
        }
        catch (Exception ex)
        {
            w.WriteLine($"{indent}<field dump failed: {ex.GetType().Name}: {ex.Message}>");
        }
    }

    
    private static List<object?> TryEnumerateIndexable(object indexable, int maxItems)
    {
        var outList = new List<object?>();
        if (indexable == null)
            return outList;

        try
        {
            var t = indexable.GetType();

            int len = -1;
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                var pLen = t.GetProperty("Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? t.GetProperty("Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pLen != null)
                {
                    var v = pLen.GetValue(indexable, index: null);
                    if (v != null)
                        len = Convert.ToInt32(v);
                }
            }
            catch
            {
                len = -1;
            }

            if (len < 0)
                len = maxItems;

            int n = Math.Min(len, maxItems);

            MethodInfo? miGet = null;
            try
            {
                GameDebugMenuBridge.TickCampHighlightTrace();
                GameDebugMenuBridge.TickWarpCatalogRecord();

                miGet = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, types: new[] { typeof(int) }, modifiers: null);
            }
            catch
            {
                miGet = null;
            }

            if (miGet == null)
            {
                // fallback: some wrappers expose an indexer as "Item"
                try
                {
                    var pi = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pi != null)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            object? item = null;
                            try { item = pi.GetValue(indexable, new object[] { i }); } catch { item = null; }
                            outList.Add(item);
                        }
                        return outList;
                    }
                }
                catch
                {
                    // ignore
                }

                return outList;
            }

            for (int i = 0; i < n; i++)
            {
                object? item = null;
                try { item = miGet.Invoke(indexable, new object[] { i }); }
                catch { item = null; }
                outList.Add(item);
            }
        }
        catch
        {
            // ignore
        }

        return outList;
    }

private static void TraverseGameObjectTree(GameObject root, int maxNodes, Action<GameObject, string> visit)
    {
        var stack = new Stack<(GameObject go, string path)>();
        stack.Push((root, SafeName(root)));

        int visited = 0;
        while (stack.Count > 0 && visited < maxNodes)
        {
            var (go, path) = stack.Pop();
            visited++;

            try { visit(go, path); }
            catch { /* ignore */ }

            Transform? tr = null;
            try { tr = go.transform; } catch { tr = null; }
            if (tr == null)
                continue;

            int childCount = 0;
            try { childCount = tr.childCount; } catch { childCount = 0; }

            // DFS: push children
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform? child = null;
                try { child = tr.GetChild(i); } catch { child = null; }
                if (child == null)
                    continue;

                GameObject? childGo = null;
                try { childGo = child.gameObject; } catch { childGo = null; }
                if (childGo == null)
                    continue;

                stack.Push((childGo, path + "/" + SafeName(childGo)));
            }
        }
    }
private static void DumpCursorMarkers(StreamWriter w, GameObject anchorGo, int maxNodes = 2000, int maxLines = 40)
{
    // Heuristic marker scan: useful when controller fields are hidden/unstable.
    // We walk the subtree and report any object names that look like cursors/highlights.
    int visited = 0;
    int printed = 0;
    int hits = 0;

    bool LooksLikeMarker(string nameLower)
    {
        return nameLower.Contains("cursor")
            || nameLower.Contains("cursur")     // typo seen in some projects
            || nameLower.Contains("select")
            || nameLower.Contains("selector")
            || nameLower.Contains("highlight")
            || nameLower.Contains("hilight")
            || nameLower.Contains("focus")
            || nameLower.Contains("arrow")
            || nameLower.Contains("marker");
    }

    TraverseGameObjectTree(anchorGo, maxNodes, (go, goPath) =>
    {
        visited++;

        if (printed >= maxLines && hits > 0)
        {
            // Still keep counting hits, but don't spam output.
        }

        string name = SafeName(go);
        string lower = name.ToLowerInvariant();
        if (!LooksLikeMarker(lower))
            return;

        hits++;

        bool active = false;
        try { active = go.activeInHierarchy; } catch { active = false; }

        if (printed < maxLines || active)
        {
            if (printed < maxLines)
            {
                printed++;
                w.WriteLine($"    [marker] active={active} path='{goPath}'");
            }
        }
    });

    if (hits > 0)
        w.WriteLine($"    [marker] hits={hits} scannedNodes={visited} (printed={printed})");
}





private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
    }
}
}