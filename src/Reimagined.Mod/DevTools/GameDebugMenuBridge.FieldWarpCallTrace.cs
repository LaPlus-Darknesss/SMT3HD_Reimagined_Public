#nullable enable

using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SMT3HD_Reimagined
{
    public partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            private static bool s_fieldWarpCallTraceInstalled;
            private static HarmonyLib.Harmony? s_fieldWarpCallTraceHarmony;

            // Installed lazily when the user enables the warp trace hotkey.
            // This is *log-only*: it never changes behavior.
            //
            // IMPORTANT: We avoid compile-time references to Il2Cpp.* wrapper types.
            // Everything is resolved by name at runtime so this stays robust across versions.
            private static void TryInstallFieldWarpCallTracePatchesOnce()
            {
                if (s_fieldWarpCallTraceInstalled)
                    return;

                s_fieldWarpCallTraceInstalled = true;

                try
                {
                    EnsureIl2CppHarmonySupportIfAvailable();

                    var fldWapType = FindTypeInLoadedAssemblies("Il2Cpp.fldWap") ?? FindTypeInLoadedAssemblies("fldWap");
                    if (fldWapType == null)
                    {
                        MelonLogger.Warning("[WarpCallTrace] fldWap type not found; call tracing disabled.");
                        return;
                    }

                    s_fieldWarpCallTraceHarmony = new HarmonyLib.Harmony("SMT3HD_Reimagined.FieldWarpCallTrace");

                    int patched = 0;

                    // Field door/warp entrypoints.
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoor_Terminal", expectedParamCount: 2);
                    patched += PatchNamedMethodOptional(fldWapType, "fldWap_Load", expectedParamCount: 3);
                    patched += PatchNamedMethodOptional(fldWapType, "fldWap_Suspend", expectedParamCount: 7);
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoor_Warp", expectedParamCount: 1);
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoor_WarpEx", expectedParamCount: 1);
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoor_ChkWarp", expectedParamCount: 0);

                    // Useful helpers for mapping.
                    patched += PatchNamedMethodOptional(fldWapType, "MakeWarpIndex", expectedParamCount: 1);
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoorRun_FindStart", expectedParamCount: 1);
                    patched += PatchNamedMethodOptional(fldWapType, "fldDoor_Start", expectedParamCount: 1);

                    if (patched > 0)
                        MelonLogger.Msg($"[WarpCallTrace] Installed fldWap call-trace patches (patched={patched}). Logs only when warp trace is enabled.");
                    else
                        MelonLogger.Warning("[WarpCallTrace] No methods patched (fldWap resolved, but expected methods were not found).");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WarpCallTrace] Failed to install patches: {ex.GetType().Name}: {ex.Message}");
                }
            }

            private static void EnsureIl2CppHarmonySupportIfAvailable()
            {
                try
                {
                    // Avoid compile-time dependency on a specific helper type name.
                    var t = Type.GetType("Il2CppInterop.HarmonySupport.HarmonySupport, Il2CppInterop.HarmonySupport");
                    var mi = t?.GetMethod("AddHarmonySupport", BindingFlags.Public | BindingFlags.Static);
                    mi?.Invoke(null, null);
                }
                catch
                {
                    // Best-effort.
                }
            }

            private static int PatchNamedMethodOptional(Type type, string methodName, int expectedParamCount)
            {
                if (s_fieldWarpCallTraceHarmony == null)
                    return 0;

                var target = FindMethodByNameAndParamCount(type, methodName, expectedParamCount);
                if (target == null)
                    return 0;

                var prefix = AccessTools.Method(typeof(GameDebugMenuBridge), nameof(CallTrace_Prefix_Generic));

                MethodInfo? postfix = null;
                if (target is MethodInfo mi)
                {
                    postfix = mi.ReturnType == typeof(void)
                        ? AccessTools.Method(typeof(GameDebugMenuBridge), nameof(CallTrace_Postfix_Void))
                        : AccessTools.Method(typeof(GameDebugMenuBridge), nameof(CallTrace_Postfix_Result));
                }

                s_fieldWarpCallTraceHarmony.Patch(
                    target,
                    prefix: prefix != null ? new HarmonyLib.HarmonyMethod(prefix) : null,
                    postfix: postfix != null ? new HarmonyLib.HarmonyMethod(postfix) : null);

                return 1;
            }

            private static MethodBase? FindMethodByNameAndParamCount(Type type, string methodName, int paramCount)
            {
                try
                {
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    foreach (var mi in type.GetMethods(flags))
                    {
                        if (!string.Equals(mi.Name, methodName, StringComparison.Ordinal))
                            continue;

                        if (mi.GetParameters().Length != paramCount)
                            continue;

                        return mi;
                    }
                }
                catch
                {
                    // ignored
                }

                return null;
            }

            // ------------------------------------------------------------
            // Patch handlers (log markers; no behavior changes)
            // ------------------------------------------------------------

            private static void CallTrace_Prefix_Generic(MethodBase __originalMethod, object[] __args)
            {
                if (!s_fieldWarpTraceEnabled)
                    return;

                var name = __originalMethod.Name;

                // MakeWarpIndex is the most valuable call: it consumes the door entry and computes/arms the warp index.
                if (string.Equals(name, "MakeWarpIndex", StringComparison.Ordinal) && __args.Length > 0 && __args[0] != null)
                {
                    string desc;
                    try { desc = DescribeDoorObjSafe(__args[0]); }
                    catch { desc = "<err>"; }

                    AppendMarker($"# CALL {name} door={desc}");
                    StartAutoCapture("CALL MakeWarpIndex", frames: 150);
                    ForceSnapshot("CallTrace pre MakeWarpIndex");
                    return;
                }

                string args = FormatArgsOneLine(__args);
                AppendMarker($"# CALL {name}{(string.IsNullOrEmpty(args) ? "" : " " + args)}");

                // Auto-capture window size depends on how big the pipeline is.
                int frames = 90;
                if (name.IndexOf("Suspend", StringComparison.OrdinalIgnoreCase) >= 0) frames = 240;
                else if (name.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0) frames = 180;
                else if (name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0) frames = 180;
                else if (name.IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0) frames = 150;

                StartAutoCapture($"CALL {name}", frames);

                // Explicit snapshots only on key transition entrypoints.
                if (name.IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0)
                    ForceSnapshot($"CallTrace pre {name}");
            }

            private static void CallTrace_Postfix_Void(MethodBase __originalMethod, object[] __args)
            {
                if (!s_fieldWarpTraceEnabled)
                    return;

                var name = __originalMethod.Name;
                AppendMarker($"# RET  {name}");

                if (name.IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(name, "MakeWarpIndex", StringComparison.Ordinal))
                {
                    ForceSnapshot($"CallTrace post {name}");
                }
            }

            private static void CallTrace_Postfix_Result(MethodBase __originalMethod, object[] __args, object __result)
            {
                if (!s_fieldWarpTraceEnabled)
                    return;

                var name = __originalMethod.Name;
                var s = __result != null ? SanitizeOneLine(__result.ToString() ?? "") : "<null>";
                AppendMarker($"# RET  {name} => {s}");

                if (name.IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0)
                    ForceSnapshot($"CallTrace post {name}");
            }

            private static string FormatArgsOneLine(object[] args)
            {
                try
                {
                    if (args == null || args.Length == 0)
                        return string.Empty;

                    int take = args.Length;
                    if (take > 6) take = 6;

                    string[] parts = new string[take];
                    for (int i = 0; i < take; i++)
                    {
                        object? a = args[i];
                        if (a == null)
                        {
                            parts[i] = "null";
                            continue;
                        }

                        string text = a.ToString() ?? "";
                        text = SanitizeOneLine(text);
                        if (text.Length > 64) text = text.Substring(0, 64) + "…";
                        parts[i] = text;
                    }

                    var joined = string.Join(", ", parts);
                    if (args.Length > take)
                        joined += ", …";

                    return $"args=[{joined}]";
                }
                catch
                {
                    return "args=[<err>]";
                }
            }
        }
    }
}
