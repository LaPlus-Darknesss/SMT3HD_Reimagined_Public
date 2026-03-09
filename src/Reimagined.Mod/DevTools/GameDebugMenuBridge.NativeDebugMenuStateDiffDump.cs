#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SMT3HD_Reimagined
{
    public sealed partial class ReimaginedMod
    {
        private static partial class GameDebugMenuBridge
        {
            // =========================================================
            // cmpTest (native debug menu) *dynamic* state mining
            // =========================================================
           

            private static Dictionary<string, string>? _cmpTestLastCensus;

            // Additional cross-type census to find where the native menu stores selection
            // state during multi-phase flows (e.g. demon-select vs skill-select).
            private static Dictionary<string, string>? _dbgCandidateLastCensus;
            private static List<Type>? _dbgCandidateTypes;

            internal static void DumpCmpTestDynamicLists(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- Dynamic Lists (best-effort) ---");

                // Known/common list statics we have already seen in dumps.
                DumpOneList(cmpTest, w, "cmpDbgStatusList", maxItems: 40);
                DumpOneList(cmpTest, w, "cmpDbgListParam", maxItems: 40);

                // Some builds expose additional lists by these names.
                DumpOneList(cmpTest, w, "cmpDbgNowList", maxItems: 60);
                DumpOneList(cmpTest, w, "gNowList", maxItems: 60);
                DumpOneList(cmpTest, w, "gSubList", maxItems: 60);

                w.WriteLine();
            }

            internal static void DumpCmpTestStaticCensusDiff(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- Static Member Census (diff since last dump) ---");

                var snap = CaptureCmpTestStaticSnapshot(cmpTest);

                if (_cmpTestLastCensus == null)
                {
                    w.WriteLine("mode=baseline (first dump this session)");
                    WriteSnapshot(w, snap, onlyChanged: false, prior: null);
                    _cmpTestLastCensus = snap;
                    w.WriteLine();

                    DumpDbgCandidateStaticCensusDiff(cmpTest, w);
                    w.WriteLine();
                    return;
                }

                w.WriteLine("mode=diff");
                WriteSnapshot(w, snap, onlyChanged: true, prior: _cmpTestLastCensus);

                // Update for next time.
                _cmpTestLastCensus = snap;
                w.WriteLine();

                DumpDbgCandidateStaticCensusDiff(cmpTest, w);
                w.WriteLine();
            }

            private static void DumpOneList(Type cmpTest, TextWriter w, string staticName, int maxItems)
            {
                if (!TryReadStaticMember(cmpTest, staticName, out object? listObj) || listObj == null)
                {
                    w.WriteLine($"{staticName}=(unavailable)");
                    return;
                }

                int len = GetIl2CppRefArrayLength(listObj);
                w.WriteLine($"{staticName}.type={listObj.GetType().FullName}");
                w.WriteLine($"{staticName}.len={len}");

                if (len <= 0)
                    return;

                int max = Math.Min(len, maxItems);

                w.WriteLine("  idx | word | Para | hasFunc | nextSize");
                w.WriteLine("  ---------------------------------------");
                for (int i = 0; i < max; i++)
                {
                    object? it = GetIl2CppRefArrayItem(listObj, i);
                    string word = Safe(it != null ? (GetStringProp(it, "word") ?? "") : "");
                    int para = GetIntProp(it, "Para");
                    bool hasFunc = it != null && GetProp(it, "func") != null;
                    int nextSize = GetIntProp(it, "nextSize");

                    w.WriteLine($"  {i,3} | {word} | {para} | {(hasFunc ? "yes" : "no"),6} | {nextSize}");
                }

                if (len > max)
                    w.WriteLine($"  ... truncated ({len - max} more)");

                w.WriteLine();
            }

            private static Dictionary<string, string> CaptureCmpTestStaticSnapshot(Type cmpTest)
            {
                var snap = new Dictionary<string, string>(StringComparer.Ordinal);

                const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var f in cmpTest.GetFields(FLAGS))
                {
                    if (f.IsLiteral) continue;
                    string name = "field:" + f.Name;

                    string value;
                    try
                    {
                        object? v = f.GetValue(null);
                        value = SummarizeValue(v);

                        if (v != null && string.Equals(f.Name, "cmpDbgPID", StringComparison.Ordinal))
                            AddCmpDbgPidDerivedKeys(snap, name, v);
                    }
                    catch (Exception ex)
                    {
                        value = $"<err:{ex.GetType().Name}>";
                    }

                    snap[name] = value;
                }

                foreach (var p in cmpTest.GetProperties(FLAGS))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length != 0) continue;

                    string name = "prop:" + p.Name;

                    string value;
                    try
                    {
                        object? v = p.GetValue(null, null);
                        value = SummarizeValue(v);

                        if (v != null && string.Equals(p.Name, "cmpDbgPID", StringComparison.Ordinal))
                            AddCmpDbgPidDerivedKeys(snap, name, v);
                    }
                    catch (Exception ex)
                    {
                        value = $"<err:{ex.GetType().Name}>";
                    }

                    snap[name] = value;
                }

                return snap;
            }

            
            // =========================================================
            // Candidate cross-type static census (Il2Cpp.*cmpDbg*)
            // =========================================================

            private static void DumpDbgCandidateStaticCensusDiff(Type cmpTest, TextWriter w)
            {
                w.WriteLine("--- Candidate Debug Statics (selection-ish Il2Cpp statics) (diff since last dump) ---");

                var snap = CaptureDbgCandidateSnapshot(cmpTest);

                // Provide a small constant header so 'no change' is still informative.
                int trackedTypes = GetDbgCandidateTypes(cmpTest).Count;
                w.WriteLine($"trackedTypes={trackedTypes} trackedKeys={snap.Count}");

                if (_dbgCandidateLastCensus == null)
                {
                    w.WriteLine("mode=baseline (first dump this session)");
                    WriteSnapshot(w, snap, onlyChanged: false, prior: null);
                    _dbgCandidateLastCensus = snap;
                    w.WriteLine();
                    return;
                }

                w.WriteLine("mode=diff");
                WriteSnapshot(w, snap, onlyChanged: true, prior: _dbgCandidateLastCensus);
                _dbgCandidateLastCensus = snap;
                w.WriteLine();
            }

            private static Dictionary<string, string> CaptureDbgCandidateSnapshot(Type cmpTest)
            {
                var snap = new Dictionary<string, string>(StringComparer.Ordinal);

                var types = GetDbgCandidateTypes(cmpTest);

                // Capture only "interesting" statics to avoid massive baseline noise.
                foreach (var t in types)
                {
                    CaptureTypeInterestingStatics(t, snap);
                }

                return snap;
            }

            
            private static List<Type> GetDbgCandidateTypes(Type cmpTest)
            {
                if (_dbgCandidateTypes != null) return _dbgCandidateTypes;

                var list = new List<Type>();

                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type[] types;
                        try
                        {
                            types = asm.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            // ex.Types can contain nulls; filter them and avoid nullable warnings.
                            var tmp = ex.Types;
                            if (tmp == null) continue;
                            var filtered = new List<Type>(tmp.Length);
                            for (int i = 0; i < tmp.Length; i++)
                            {
                                var t0 = tmp[i];
                                if (t0 != null) filtered.Add(t0);
                            }
                            types = filtered.ToArray();
                        }
                        catch
                        {
                            continue;
                        }

                        for (int i = 0; i < types.Length; i++)
                        {
                            var t = types[i];
                            if (t == null) continue;
                            if (t == cmpTest) continue;

                            var full = t.FullName;
                            if (string.IsNullOrEmpty(full)) continue;

                            // We focus on Il2Cpp generated types; they tend to hold native debug state.
                            if (!full.StartsWith("Il2Cpp", StringComparison.Ordinal)) continue;

                            // Further narrow to menu/camp classes. This avoids huge noise from unrelated Il2Cpp singletons.
                            if (full.IndexOf("cmp", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            // cmpCalc tends to be a constants-heavy helper with little selection state; skip to reduce clutter.
                            if (full.IndexOf("cmpCalc", StringComparison.OrdinalIgnoreCase) >= 0)
                                continue;

                            if (!HasCandidateStatics(t))
                                continue;

                            list.Add(t);
                        }
                    }
                }
                catch
                {
                    // Best-effort: if reflection fails, we still return what we have.
                }

                // Keep this bounded so dumps stay fast and readable.
                list.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
                const int MAX_TYPES = 60;
                if (list.Count > MAX_TYPES)
                    list.RemoveRange(MAX_TYPES, list.Count - MAX_TYPES);

                _dbgCandidateTypes = list;
                return list;
            }

            private static bool HasCandidateStatics(Type t)
            {
                const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                try
                {
                    foreach (var f in t.GetFields(FLAGS))
                    {
                        if (f.IsLiteral) continue;
                        if (IsNoiseMemberName(f.Name)) continue;
                        if (!IsInterestingValueType(f.FieldType)) continue;
                        if (IsCandidateMemberName(f.Name)) return true;
                    }

                    foreach (var p in t.GetProperties(FLAGS))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue;
                        if (IsNoiseMemberName(p.Name)) continue;
                        if (!IsInterestingValueType(p.PropertyType)) continue;
                        if (IsCandidateMemberName(p.Name)) return true;
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }

            private static bool IsCandidateMemberName(string name)
            {
                if (string.IsNullOrEmpty(name)) return false;

                // We’re trying to find “what changes when I move highlight”, so we bias toward
                // names that look like cursor/selection/state/id/target/work variables.
                var n = name;

                // Common selection/state stems
                if (n.IndexOf("sel", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("cursor", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("now", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("work", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("list", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // "list" alone is extremely noisy (e.g. FILE_LIST_* constants). Only keep if it looks like a cursor/list-length variable.
                    if (n.IndexOf("num", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("cursor", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("sel", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }

                // Domain hints
                if (n.IndexOf("devil", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                // Also capture id-like names (but avoid pure noise by requiring "id" token)
                if (n.Equals("id", StringComparison.OrdinalIgnoreCase)) return true;
                if (n.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) return true;
                if (n.IndexOf("_id", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                // PID wrapper
                if (n.IndexOf("PID", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                return false;
            }


            private static bool HasInterestingStatics(Type t)
            {
                const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                try
                {
                    foreach (var f in t.GetFields(FLAGS))
                    {
                        if (f.IsLiteral) continue;
                        if (IsNoiseMemberName(f.Name)) continue;
                        if (IsInterestingValueType(f.FieldType)) return true;
                    }

                    foreach (var p in t.GetProperties(FLAGS))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue;
                        if (IsNoiseMemberName(p.Name)) continue;

                        if (IsInterestingValueType(p.PropertyType)) return true;
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }

            
            private static void CaptureTypeInterestingStatics(Type t, Dictionary<string, string> outSnap)
            {
                const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                // Prefix keys by type to avoid collisions.
                string prefix = t.FullName ?? t.Name ?? "<type>";

                try
                {
                    foreach (var f in t.GetFields(FLAGS))
                    {
                        if (f.IsLiteral) continue;
                        if (IsNoiseMemberName(f.Name)) continue;
                        if (!IsInterestingValueType(f.FieldType)) continue;
                        if (!IsCandidateMemberName(f.Name)) continue;

                        string key = prefix + ".field:" + f.Name;

                        try
                        {
                            object? v = f.GetValue(null);
                            outSnap[key] = SummarizeValue(v);

                            TryAddProcessIdDerivedKeys(outSnap, prefix + ".field:" + f.Name, v);
                        }
                        catch (Exception ex)
                        {
                            outSnap[key] = $"<err:{ex.GetType().Name}>";
                        }
                    }

                    foreach (var p in t.GetProperties(FLAGS))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue;
                        if (IsNoiseMemberName(p.Name)) continue;
                        if (!IsInterestingValueType(p.PropertyType)) continue;
                        if (!IsCandidateMemberName(p.Name)) continue;

                        string key = prefix + ".prop:" + p.Name;

                        try
                        {
                            object? v = p.GetValue(null, null);
                            outSnap[key] = SummarizeValue(v);

                            TryAddProcessIdDerivedKeys(outSnap, prefix + ".prop:" + p.Name, v);
                        }
                        catch (Exception ex)
                        {
                            outSnap[key] = $"<err:{ex.GetType().Name}>";
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            private static bool IsNoiseMemberName(string name)
            {
                if (string.IsNullOrEmpty(name)) return true;
                if (name.StartsWith("NativeFieldInfoPtr_", StringComparison.Ordinal)) return true;
                if (name.StartsWith("NativeMethodInfoPtr_", StringComparison.Ordinal)) return true;
                if (name.StartsWith("NativePropertyInfoPtr_", StringComparison.Ordinal)) return true;
                if (name.StartsWith("NativeTypeInfoPtr_", StringComparison.Ordinal)) return true;
                if (name.StartsWith("NativeObjectPtr_", StringComparison.Ordinal)) return true;
                // Many Il2Cpp wrappers expose lots of constants; try to exclude obvious constant-ish symbols.
                // (We keep this conservative; we still want selection indices.)
                if (name.Length >= 6)
                {
                    bool hasLower = false;
                    bool hasUpper = false;
                    for (int i = 0; i < name.Length; i++)
                    {
                        char c = name[i];
                        if (c >= 'a' && c <= 'z') hasLower = true;
                        else if (c >= 'A' && c <= 'Z') hasUpper = true;
                    }

                    // If it's ALL UPPER and contains '_' it's very likely a constant.
                    if (!hasLower && hasUpper && name.IndexOf('_') >= 0)
                        return true;
                }

                return false;
            }

            private static bool IsInterestingValueType(Type t)
            {
                if (t == typeof(string)) return true;
                if (t.IsEnum) return true;

                // Common numeric primitives.
                if (t == typeof(bool) ||
                    t == typeof(byte) || t == typeof(sbyte) ||
                    t == typeof(short) || t == typeof(ushort) ||
                    t == typeof(int) || t == typeof(uint) ||
                    t == typeof(long) || t == typeof(ulong))
                    return true;

                // We intentionally ignore IntPtr and pointer-like wrappers to reduce noise.

                // Some Il2Cpp structs show up as ValueType; we only care if it looks like dds3ProcessID_t.
                if (t.IsValueType)
                {
                    var n = t.FullName ?? t.Name ?? string.Empty;
                    if (n.IndexOf("dds3ProcessID_t", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }

                return false;
            }

            private static void TryAddProcessIdDerivedKeys(Dictionary<string, string> snap, string baseKey, object? v)
            {
                if (v == null) return;

                var vt = v.GetType();
                var n = vt.FullName ?? vt.Name ?? string.Empty;

                if (n.IndexOf("dds3ProcessID_t", StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                // Reuse existing helper: derived keys become "{baseKey}.id" etc.
                try
                {
                    AddCmpDbgPidDerivedKeys(snap, baseKey, v);
                }
                catch
                {
                    // ignore
                }
            }
            private static void AddCmpDbgPidDerivedKeys(Dictionary<string, string> snap, string baseKey, object v)
            {
                try
                {
                    if (TryGetInstanceMemberValue(v, "id", out object? idObj) && idObj != null)
                        snap[baseKey + ".id"] = SummarizeValue(idObj);

                    if (TryGetInstanceMemberValue(v, "namehash", out object? nhObj) && nhObj != null)
                        snap[baseKey + ".namehash"] = SummarizeValue(nhObj);

                    if (TryGetInstanceMemberValue(v, "name", out object? nameObj) && nameObj != null)
                    {
                        string s = DecodeByteArrayBestEffort(nameObj, 64);
                        if (string.IsNullOrEmpty(s))
                            snap[baseKey + ".name"] = "<blank>";
                        else
                            snap[baseKey + ".name"] = "\"" + Safe(s) + "\"";
                    }
                }
                catch
                {
                    // best-effort only
                }
            }

            private static string SummarizeValue(object? v)
            {
                if (v == null) return "null";

                try
                {
                    // Strings: sanitize + shorten.
                    if (v is string s)
                    {
                        s = Safe(s);
                        if (s.Length > 120) s = s.Substring(0, 120) + "...";
                        return $"\"{s}\"";
                    }

                    // Primitive-ish.
                    if (v is bool b) return b ? "true" : "false";
                    if (v is byte or sbyte or short or ushort or int or uint or long or ulong or float or double)
                        return v.ToString() ?? v.GetType().Name;

                    if (v is Enum)
                    {
                        long n = Convert.ToInt64(v);
                        return $"{n} ({v})";
                    }

                    // If it looks like an Il2Cpp reference array or other collection, include length/count.
                    if (TryGetLengthOrCount(v, out int n2, out string kind2))
                    {
                        return $"{v.GetType().Name} {kind2}={n2}";
                    }

                    // Try to include a pointer if available (Il2CppObjectBase often has Pointer).
                    var t = v.GetType();
                    var pPtr = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pPtr != null && pPtr.PropertyType == typeof(IntPtr))
                    {
                        var ptr = (IntPtr)pPtr.GetValue(v, null)!;
                        return $"{t.Name} ptr=0x{ptr.ToInt64():X}";
                    }

                    return t.FullName ?? t.Name;
                }
                catch
                {
                    return v.GetType().Name;
                }
            }

            private static void WriteSnapshot(TextWriter w, Dictionary<string, string> snap, bool onlyChanged, Dictionary<string, string>? prior)
            {
                int changed = 0;

                foreach (var kv in snap)
                {
                    string key = kv.Key;
                    string now = kv.Value;

                    string? before = null;
                    bool hadBefore = prior != null && prior.TryGetValue(key, out before);

                    bool isChanged = !hadBefore || before != now;

                    if (onlyChanged && !isChanged)
                        continue;

                    if (isChanged) changed++;

                    {
                        string suffixNow = ResolveKnownNameSuffix(key, now);
                        if (!hadBefore)
                            w.WriteLine($"+ {key}={now}{suffixNow}");
                        else
                            w.WriteLine($"~ {key}={before} -> {now}{suffixNow}");
                    }
                }

                if (onlyChanged)
                    w.WriteLine($"changedCount={changed}");
                else
                    w.WriteLine($"entryCount={snap.Count}");
            }

            private static string ResolveKnownNameSuffix(string key, string value)
            {
                try
                {
                    if (key != null && key.IndexOf("Skill_id", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (int.TryParse(value, out int id) && id >= 0)
                        {
                            // Most of the time, cmpTest.Skill_id really is a skill id.
                            if (TryGetSkillNameBestEffort(id, 0, out var skillName))
                                return $" (\"{(string.IsNullOrEmpty(skillName) ? "<blank>" : skillName)}\")";

                            // However, in some phases (notably "select a demon" inside SKILL),
                            // the same static is reused as a devil id. Add a second, clearly-labeled hint.
                            if (TryGetDevilNameBestEffort(id, out var devilName))
                                return $" (devil? \"{(string.IsNullOrEmpty(devilName) ? "<blank>" : devilName)}\")";
                        }
                    }
                }
                catch
                {
                    // ignore
                }
                return "";
            }

        }
    }
}
