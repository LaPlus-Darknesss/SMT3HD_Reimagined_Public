#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // Field probes are intentionally reflection-based:
            // - We do not rely on generated Il2Cpp bindings for game types.
            // - We can ship tools that survive minor type layout changes.

            public static bool TryGetFieldMapHudLines(bool dumpMode, out string[] lines)
            {
                var outLines = new List<string>(capacity: dumpMode ? 24 : 12);

                try
                {
                    // Always include Unity scene context.
                    string sceneName = "";
                    try
                    {
                        var sc = SceneManager.GetActiveScene();
                        sceneName = sc.IsValid() ? sc.name : "<invalid>";
                    }
                    catch { /* ignore */ }

                    if (!string.IsNullOrEmpty(sceneName))
                        outLines.Add($"scene=\"{San(sceneName)}\"");

                    // Grab fldSceneParam (if present, we're almost certainly in the field).
                    object? sceneParam = TryGetFldSceneParam(out string spErr);
                    if (sceneParam == null)
                    {
                        // If we cannot access fldSceneParam, treat this as "not in field".
                        lines = Array.Empty<string>();
                        return false;
                    }

                    // Basic numeric context.
                    int loadField = GetInt(sceneParam, "loadField", fallback: -1);
                    int loadArea = GetInt(sceneParam, "loadArea", fallback: -1);
                    int loadScript = GetInt(sceneParam, "loadScript", fallback: -1);
                    int callMode = GetInt(sceneParam, "callMode", fallback: -1);
                    int camMode = GetInt(sceneParam, "camMode", fallback: -1);
                    int bgm = GetInt(sceneParam, "bgm", fallback: -1);

                    outLines.Add($"loadField={loadField} loadArea={loadArea} loadScript={loadScript} callMode={callMode} camMode={camMode} bgm={bgm}");

                    // Human-readable names via fldNameData (best-effort).
                    string nameField = "";
                    string nameArea = "";
                    try
                    {
						if (TryGetFldNameData(out object? nameDataObj) && nameDataObj != null)
						{
							nameField = TryInvokeString1(nameDataObj!, "GetFieldName", loadField) ?? "";
							nameArea = TryInvokeString1(nameDataObj!, "GetAreaName", loadArea) ?? "";
						}
                    }
                    catch { /* ignore */ }

                    if (!string.IsNullOrEmpty(nameField) || !string.IsNullOrEmpty(nameArea))
                        outLines.Add($"nameField=\"{San(nameField)}\" nameArea=\"{San(nameArea)}\"");

                    // Common string-ish fields in fldSceneParam.
                    string pointRes = GetMaybeFixedCharString(sceneParam, "pointResName", 96);
                    string lastLoadRes = GetMaybeFixedCharString(sceneParam, "lastLoadRes", 96);
                    string lastLoadBgmRes = GetMaybeFixedCharString(sceneParam, "lastLoadBgmRes", 96);
                    string lastLoadSeRes = GetMaybeFixedCharString(sceneParam, "lastLoadSeRes", 96);
                    string camName = GetMaybeFixedCharString(sceneParam, "camName", 96);

                    if (dumpMode)
                    {
                        if (!string.IsNullOrEmpty(pointRes)) outLines.Add($"pointRes=\"{San(pointRes)}\"");
                        if (!string.IsNullOrEmpty(lastLoadRes)) outLines.Add($"lastLoadRes=\"{San(lastLoadRes)}\"");
                        if (!string.IsNullOrEmpty(lastLoadBgmRes)) outLines.Add($"lastLoadBgmRes=\"{San(lastLoadBgmRes)}\"");
                        if (!string.IsNullOrEmpty(lastLoadSeRes)) outLines.Add($"lastLoadSeRes=\"{San(lastLoadSeRes)}\"");
                        if (!string.IsNullOrEmpty(camName)) outLines.Add($"camName=\"{San(camName)}\"");
                    }

                    // AutoMap UI names (best-effort; only meaningful when AutoMap UI is active).
                    try
                    {
                        string jp = "";
                        string en = "";

                        var autoMapType = FindGameType("Il2Cpp.autoMapUI", "autoMapUI");
                        if (autoMapType != null)
                        {
                            var ui = FindObjectOfTypeCompat(autoMapType);
                            if (ui != null)
                            {
                                // Many Unity UI text components expose "text".
                                object? fldName = GetMember(ui, "fldName");
                                object? fldNameE = GetMember(ui, "fldName_e");

                                jp = GetUnityTextValue(fldName);
                                en = GetUnityTextValue(fldNameE);
                            }
                        }

                        if (!string.IsNullOrEmpty(jp) || !string.IsNullOrEmpty(en))
                            outLines.Add($"autoMap: jp=\"{San(jp)}\" en=\"{San(en)}\"");
                    }
                    catch { /* ignore */ }

                    // Warp buffer presence / door count (best-effort).
                    try
                    {
                        if (TryGetDoorBuff(out var doorBuff, out int doorCount))
                        {
                            outLines.Add($"warpBuff: doorCount={doorCount}");
                        }
                    }
                    catch { /* ignore */ }

                    // Warp state snapshot (best-effort; helps debug whether warps are arming).
                    try
                    {
                        var tWap = FindGameType("Il2Cpp.fldWap", "fldWap");
                        if (tWap != null)
                        {
                            int gWarpIdx = TryReadStaticInt(tWap, "gFldWarpIdx", fallback: int.MinValue);
                            int suspendIdx = TryReadStaticInt(tWap, "gSuspendWarpIndex", fallback: int.MinValue);
                            int panel = TryReadStaticInt(tWap, "gFldWapPanel", fallback: int.MinValue);
                            int afterNext = TryReadStaticInt(tWap, "gFldWap_after_nextidx", fallback: int.MinValue);
                            int afterFlag = TryReadStaticInt(tWap, "gFldWap_after_flag", fallback: int.MinValue);
                            if (gWarpIdx != int.MinValue || suspendIdx != int.MinValue || panel != int.MinValue)
                                outLines.Add($"warpState: gWarpIdx={gWarpIdx} suspendIdx={suspendIdx} panel={panel} afterNext={afterNext} afterFlag={afterFlag}");
                        }
                    }
                    catch { /* ignore */ }

                    // Warp catalog summary (best-effort): shows known routes from the current context once a catalog exists.
                    try
                    {
                        if (TryGetWarpCatalogHudLines(loadField, loadArea, loadScript, out var catalogLines))
                        {
                            for (int i = 0; i < catalogLines.Length; i++)
                                outLines.Add(catalogLines[i]);
                        }
                    }
                    catch { /* ignore */ }

                    lines = outLines.ToArray();
                    return true;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Reimagined] TryGetFieldMapHudLines failed: {ex.GetType().Name}: {ex.Message}");
                    lines = Array.Empty<string>();
                    return false;
                }
            }

            // ----- Reflection helpers (field / warp) -----

            private static Type? FindGameType(params string[] fullNames)
            {
                for (int i = 0; i < fullNames.Length; i++)
                {
                    string n = fullNames[i];
                    if (string.IsNullOrWhiteSpace(n))
                        continue;

                    var t = FindTypeInLoadedAssemblies(n);
                    if (t != null)
                        return t;
                }
                return null;
            }

            private static object? TryGetFldSceneParam(out string err)
            {
                err = "";
                try
                {
                    var t = FindGameType("Il2Cpp.fldMain", "fldMain");
                    if (t == null)
                    {
                        err = "type fldMain not found";
                        return null;
                    }

                    var sp = GetStaticMemberValueLoose(t, "fldSceneParam");
                    if (sp == null)
                    {
                        err = "fldSceneParam null";
                        return null;
                    }

                    return sp;
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    return null;
                }
            }

            private static bool TryGetFldNameData(out object? nameData)
            {
                nameData = null;
                try
                {
                    var t = FindGameType("Il2Cpp.fldGlobal", "fldGlobal");
                    if (t == null)
                        return false;

                    nameData = GetStaticMemberValueLoose(t, "fldNameData");
                    return nameData != null;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryGetDoorBuff(out object? doorBuff, out int count)
            {
                doorBuff = null;
                count = -1;

                var t = FindGameType("Il2Cpp.fldWap", "fldWap");
                if (t == null)
                    return false;

                object? warpBuff = GetStaticMemberValueLoose(t, "gFldWarpBuff");
                if (warpBuff == null)
                    return false;

                doorBuff = GetMember(warpBuff, "door_buff");
                if (doorBuff == null)
                    return false;

                if (!TryGetArrayLength(doorBuff, out count))
                    count = -1;

                return true;
            }

            private static object? GetStaticMemberValueLoose(Type t, string memberName)
            {
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                var f = t.GetField(memberName, BF);
                if (f != null)
                    return f.GetValue(null);

                var p = t.GetProperty(memberName, BF);
                if (p != null)
                    return p.GetValue(null, null);

                return null;
            }

            private static object? GetMember(object obj, string memberName)
            {
                if (obj == null)
                    return null;

                var t = obj.GetType();
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var f = t.GetField(memberName, BF);
                if (f != null)
                    return f.GetValue(obj);

                var p = t.GetProperty(memberName, BF);
                if (p != null)
                    return p.GetValue(obj, null);

                return null;
            }

            private static int GetInt(object obj, string memberName, int fallback)
            {
                try
                {
                    object? v = GetMember(obj, memberName);
                    if (v == null)
                        return fallback;

                    if (v is int i) return i;
                    if (v is short s) return s;
                    if (v is byte b) return b;
                    if (v is sbyte sb) return sb;
                    if (v is uint ui) return unchecked((int)ui);
                    if (v is long l) return unchecked((int)l);

                    return Convert.ToInt32(v);
                }
                catch
                {
                    return fallback;
                }
            }

            private static string? TryInvokeString1(object? instance, string methodName, int arg0)
            {
                if (instance == null)
                    return null;

                var t = instance.GetType();
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                foreach (var m in t.GetMethods(BF))
                {
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        continue;

                    var ps = m.GetParameters();
                    if (ps.Length != 1)
                        continue;

                    try
                    {
                        object? ret = m.Invoke(instance, new object[] { arg0 });
                        return ret?.ToString();
                    }
                    catch
                    {
                        // keep trying overloads
                    }
                }

                return null;
            }

            // NOTE: TryGetArrayLength is defined in the shared GameDebugMenuBridge.cs helpers.
            // Keep a single canonical implementation to avoid partial-class duplicate member errors.

            private static bool TryGetArrayElement(object arr, int index, out object? elem)
            {
                elem = null;
                if (arr == null)
                    return false;

                try
                {
                    if (arr is Array a)
                    {
                        if (index < 0 || index >= a.Length)
                            return false;
                        elem = a.GetValue(index);
                        return elem != null;
                    }

                    var t = arr.GetType();

                    // Common pattern: get_Item(int)
                    var mi = t.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mi != null)
                    {
                        var ps = mi.GetParameters();
                        if (ps.Length == 1)
                        {
                            elem = mi.Invoke(arr, new object[] { index });
                            return elem != null;
                        }
                    }

                    // Indexer property: Item[int]
                    var pi = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pi != null)
                    {
                        var ps = pi.GetIndexParameters();
                        if (ps.Length == 1)
                        {
                            elem = pi.GetValue(arr, new object[] { index });
                            return elem != null;
                        }
                    }

                    // Fallback: method Get(int)
                    var mi2 = t.GetMethod("Get", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mi2 != null)
                    {
                        var ps = mi2.GetParameters();
                        if (ps.Length == 1)
                        {
                            elem = mi2.Invoke(arr, new object[] { index });
                            return elem != null;
                        }
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }

            private static string GetUnityTextValue(object? maybeText)
            {
                if (maybeText == null)
                    return "";

                try
                {
                    // UnityEngine.UI.Text or TMPro TMP_Text both use "text".
                    var t = maybeText.GetType();
                    var p = t.GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null)
                        return p.GetValue(maybeText, null)?.ToString() ?? "";

                    // Some wrappers expose get_text()
                    var m = t.GetMethod("get_text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null)
                        return m.Invoke(maybeText, null)?.ToString() ?? "";
                }
                catch { /* ignore */ }

                return "";
            }

            private static string GetMaybeFixedCharString(object obj, string memberName, int maxLen)
            {
                try
                {
                    object? v = GetMember(obj, memberName);
                    if (v == null)
                        return "";

                    if (v is string s)
                        return s;

                    if (v is Il2CppStructArray<char> carr)
                        return SafeCharArray(carr, maxLen);

                    return v.ToString() ?? "";
                }
                catch
                {
                    return "";
                }
            }
            // Convenience overload: many callsites assume a sane default max length.
            private static string GetMaybeFixedCharString(object obj, string memberName)
            {
                return GetMaybeFixedCharString(obj, memberName, 96);
            }



            private static string SafeCharArray(Il2CppStructArray<char>? arr, int maxLen)
            {
                if (arr == null)
                    return "";

                int n = Math.Min(arr.Length, maxLen);
                int end = 0;
                for (; end < n; end++)
                {
                    char c = arr[end];
                    if (c == '\0')
                        break;
                }

                if (end <= 0)
                    return "";

                var sb = new StringBuilder(end);
                for (int i = 0; i < end; i++)
                    sb.Append(arr[i]);

                return sb.ToString();
            }

            private static string San(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return "";

                // Keep dumps single-line friendly.
                var sb = new StringBuilder(s.Length);
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '\r' || c == '\n')
                    {
                        sb.Append(' ');
                        continue;
                    }
                    if (char.IsControl(c))
                        continue;
                    sb.Append(c);
                }
                return sb.ToString().Trim();
            }
        }
    }
}
