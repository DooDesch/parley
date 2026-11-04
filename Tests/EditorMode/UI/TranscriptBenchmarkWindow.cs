using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Tests.EditorMode
{
    /// <summary>
    /// Measures how long the transcript takes per frame while a long reply streams in and while a long chat is
    /// scrolled. Each step applies one batch of streamed text, runs the transcript refresh and then repaints the
    /// window synchronously, so the timings include UI Toolkit layout, text generation and rendering.
    /// </summary>
    internal sealed class TranscriptBenchmarkWindow : EditorWindow
    {
        private const int HistoryTurns = 30;
        private const int StreamChunkChars = 80;
        private const float ScrollStepPixels = 120.0f;
        private const int MaxScrollSteps = 400;
        private const string LogPrefix = "[ParleyBench] ";

        private static readonly MethodInfo RepaintImmediatelyMethod = typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo FlushDeltasMethod = typeof(ChatSession).GetMethod("FlushDeltas", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo FlushDirtyMethod = typeof(TranscriptView).GetMethod("FlushDirty", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<double> _streamUpdate = new ();
        private readonly List<double> _streamRepaint = new ();
        private readonly List<double> _scrollRepaint = new ();
        private readonly StringBuilder _report = new ();

        private ChatSession _session;
        private TranscriptView _transcript;
        private string _stream;
        private int _streamOffset;
        private int _scrollSteps;
        private int _phase;
        private int _settleFrames;
        private double _bindMs;
        private double _firstRepaintMs;

        [MenuItem("Tools/Parley/Run Transcript Benchmark")]
        public static void Run()
        {
            if (RepaintImmediatelyMethod == null || FlushDeltasMethod == null || FlushDirtyMethod == null)
            {
                Debug.LogError(LogPrefix + "A required internal method was not found; the benchmark cannot run.");
                return;
            }

            TranscriptBenchmarkWindow window = CreateInstance<TranscriptBenchmarkWindow>();
            window.titleContent = new GUIContent("Parley Benchmark");
            window.ShowUtility();
            window.position = new Rect(120, 80, 440, 760);
            window.Begin();
        }

        private void Begin()
        {
            ParleyStyles.Apply(rootVisualElement);
            rootVisualElement.style.flexGrow = 1;
            ParleyProfile profile = ParleyProfile.CreateClaude("Benchmark", ClaudeAuthMethod.CliDefault);
            SessionRecord record = new SessionRecord();
            for (int turn = 0; turn < HistoryTurns; turn++)
            {
                AddHistoryTurn(record, turn);
            }

            _session = new ChatSession(profile, record, true);
            _transcript = new TranscriptView();
            _transcript.style.flexGrow = 1;
            rootVisualElement.Add(_transcript);
            Stopwatch watch = Stopwatch.StartNew();
            _transcript.Bind(_session);
            _bindMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            RepaintNow();
            _firstRepaintMs = watch.Elapsed.TotalMilliseconds;
            _stream = BuildStream();
            _phase = 0;
            _settleFrames = 10;
            EditorApplication.update += Tick;
            Debug.Log(LogPrefix + "Started: " + HistoryTurns + " history turns, " + _stream.Length + " characters to stream.");
        }

        private void Tick()
        {
            try
            {
                Step();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(LogPrefix + "The benchmark stopped in phase " + _phase + ".");
                Finish();
            }
        }

        private void Step()
        {
            if (_settleFrames > 0)
            {
                _settleFrames--;
                return;
            }

            switch (_phase)
            {
                case 0:
                    StreamStep();
                    break;
                case 1:
                    ScrollStep();
                    break;
                default:
                    Finish();
                    break;
            }
        }

        private void StreamStep()
        {
            if (_streamOffset >= _stream.Length)
            {
                ((IAgentSink)_session).BlockFinalized(new BlockFinalizeRequest("bench-text", BlockKind.Text, null, _stream, null, null, null));
                FlushDirtyMethod.Invoke(_transcript, null);
                RepaintNow();
                _phase = 1;
                _settleFrames = 10;
                return;
            }

            int length = Math.Min(StreamChunkChars, _stream.Length - _streamOffset);
            string chunk = _stream.Substring(_streamOffset, length);
            _streamOffset += length;
            Stopwatch watch = Stopwatch.StartNew();
            ((IAgentSink)_session).TextDelta("bench-text", chunk);
            FlushDeltasMethod.Invoke(_session, null);
            FlushDirtyMethod.Invoke(_transcript, null);
            _streamUpdate.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            RepaintNow();
            _streamRepaint.Add(watch.Elapsed.TotalMilliseconds);
        }

        private void ScrollStep()
        {
            ScrollView scroll = _transcript.Q<ScrollView>(className: "pl-transcript__scroll");
            if (scroll == null || scroll.scrollOffset.y <= 0.0f || _scrollSteps >= MaxScrollSteps)
            {
                _phase = 2;
                return;
            }

            _scrollSteps++;
            scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, Mathf.Max(0.0f, scroll.scrollOffset.y - ScrollStepPixels));
            Stopwatch watch = Stopwatch.StartNew();
            RepaintNow();
            _scrollRepaint.Add(watch.Elapsed.TotalMilliseconds);
        }

        private void Finish()
        {
            EditorApplication.update -= Tick;
            int elements = CountElements(_transcript);
            string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChatSession).Assembly)?.resolvedPath ?? "unknown";
            _report.AppendLine("# Parley transcript benchmark");
            _report.AppendLine();
            _report.AppendLine("- Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            _report.AppendLine("- Package: " + package);
            _report.AppendLine("- Unity: " + Application.unityVersion);
            _report.AppendLine("- Window: " + position.width + " x " + position.height);
            _report.AppendLine("- History turns: " + HistoryTurns + ", streamed characters: " + _stream.Length + " in chunks of " + StreamChunkChars);
            _report.AppendLine("- Visual elements in transcript at the end: " + elements);
            _report.AppendLine("- Bind of history: " + _bindMs.ToString("F1") + " ms, first repaint: " + _firstRepaintMs.ToString("F1") + " ms");
            _report.AppendLine();
            _report.AppendLine("| Phase | Steps | Mean ms | Median ms | P95 ms | Max ms | Total s |");
            _report.AppendLine("|---|---|---|---|---|---|---|");
            List<double> streamTotal = new List<double>();
            for (int i = 0; i < _streamUpdate.Count; i++)
            {
                streamTotal.Add(_streamUpdate[i] + _streamRepaint[i]);
            }

            Row("Stream: update", _streamUpdate);
            Row("Stream: repaint", _streamRepaint);
            Row("Stream: frame", streamTotal);
            Row("Stream: frame, last 25%", streamTotal.GetRange(streamTotal.Count * 3 / 4, streamTotal.Count - streamTotal.Count * 3 / 4));
            Row("Scroll: frame", _scrollRepaint);
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "ParleyBench");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "bench-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".md");
            File.WriteAllText(file, _report.ToString());
            Debug.Log(LogPrefix + "Finished. Report written to " + file + "\n" + _report);
            _transcript.Unbind();
            _session.Dispose();
            Close();
        }

        private void Row(string name, List<double> values)
        {
            if (values.Count == 0)
            {
                _report.AppendLine("| " + name + " | 0 | - | - | - | - | - |");
                return;
            }

            List<double> sorted = new List<double>(values);
            sorted.Sort();
            double sum = 0.0;
            foreach (double value in sorted)
            {
                sum += value;
            }

            double median = sorted[sorted.Count / 2];
            double p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
            _report.AppendLine("| " + name + " | " + sorted.Count + " | " + (sum / sorted.Count).ToString("F2") + " | " + median.ToString("F2") + " | " + p95.ToString("F2")
                + " | " + sorted[sorted.Count - 1].ToString("F2") + " | " + (sum / 1000.0).ToString("F1") + " |");
        }

        private void RepaintNow()
        {
            RepaintImmediatelyMethod.Invoke(this, null);
        }

        private static int CountElements(VisualElement element)
        {
            int count = 1;
            foreach (VisualElement child in element.hierarchy.Children())
            {
                count += CountElements(child);
            }

            return count;
        }

        private static void AddHistoryTurn(SessionRecord record, int turn)
        {
            TranscriptEntry user = new TranscriptEntry { Role = EntryRole.User };
            user.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Text, Text = "Turn " + turn + ": please refactor the `PlayerController` and explain what changed.", IsFinished = true });
            record.Entries.Add(user);
            TranscriptEntry assistant = new TranscriptEntry { Role = EntryRole.Assistant };
            assistant.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Thinking, Text = Repeat("Looking at the controller and its callers. ", 12), IsFinished = true });
            assistant.Blocks.Add(Tool("Read", "read-" + turn, new JObject { ["file_path"] = "Assets/Scripts/PlayerController.cs" }, CodeLines(120)));
            assistant.Blocks.Add(Tool("Grep", "grep-" + turn, new JObject { ["pattern"] = "PlayerController", ["path"] = "Assets" }, Repeat("Assets/Scripts/Foo.cs:12: PlayerController player;\n", 20)));
            assistant.Blocks.Add(Tool("Edit", "edit-" + turn, new JObject
            {
                ["file_path"] = "Assets/Scripts/PlayerController.cs",
                ["old_string"] = CodeLines(14),
                ["new_string"] = CodeLines(14).Replace("speed", "moveSpeed"),
            }, "The file has been updated."));
            assistant.Blocks.Add(Tool("Bash", "bash-" + turn, new JObject { ["command"] = "dotnet build", ["description"] = "Build the project" }, Repeat("Build succeeded. 0 warnings.\n", 30)));
            assistant.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Text, Text = Answer(turn), IsFinished = true });
            record.Entries.Add(assistant);
        }

        private static TranscriptBlock Tool(string name, string id, JObject input, string result)
        {
            return new TranscriptBlock { Kind = BlockKind.ToolUse, ToolName = name, ToolUseId = id, Input = input, Result = result, IsFinished = true };
        }

        private static string Answer(int turn)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("## Summary " + turn);
            text.AppendLine();
            text.AppendLine("I split the **movement** and the *input handling* so `PlayerController` only reads input. The physics now runs in `FixedUpdate`, which removes the jitter at low frame rates.");
            text.AppendLine();
            for (int i = 0; i < 6; i++)
            {
                text.AppendLine("- Item " + i + ": renamed `speed` to `moveSpeed` and moved it into the settings asset.");
            }

            text.AppendLine();
            text.AppendLine("```csharp");
            text.Append(CodeLines(25));
            text.AppendLine("```");
            text.AppendLine();
            text.AppendLine("| File | Change |");
            text.AppendLine("|---|---|");
            text.AppendLine("| PlayerController.cs | input only |");
            text.AppendLine("| PlayerMotor.cs | new |");
            text.AppendLine();
            text.AppendLine("Run the scene once and check that the player still stops at walls.");
            return text.ToString();
        }

        private static string BuildStream()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Here is the full plan, followed by the code and the list you asked for.");
            text.AppendLine();
            text.AppendLine("### Plan");
            text.AppendLine();
            for (int i = 0; i < 20; i++)
            {
                text.AppendLine((i + 1) + ". Step " + i + ": update `Component" + i + "` and keep the **public API** stable.");
            }

            text.AppendLine();
            text.AppendLine("```csharp");
            text.Append(CodeLines(160));
            text.AppendLine("```");
            text.AppendLine();
            for (int i = 1; i <= 1200; i++)
            {
                text.AppendLine("line " + i + " of the long answer");
            }

            text.AppendLine();
            text.AppendLine("That is everything.");
            return text.ToString();
        }

        private static string CodeLines(int count)
        {
            StringBuilder code = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                switch (i % 5)
                {
                    case 0:
                        code.AppendLine("    // Moves the player by the current input " + i);
                        break;
                    case 1:
                        code.AppendLine("    private float speed = " + i + ".5f;");
                        break;
                    case 2:
                        code.AppendLine("    public void Move(Vector3 direction) { transform.position += direction * speed * Time.deltaTime; }");
                        break;
                    case 3:
                        code.AppendLine("    if (direction.sqrMagnitude > 0.01f) { string label = \"moving\"; Debug.Log(label); }");
                        break;
                    default:
                        code.AppendLine("    return;");
                        break;
                }
            }

            return code.ToString();
        }

        private static string Repeat(string text, int count)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                builder.Append(text);
            }

            return builder.ToString();
        }
    }
}
