using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    // Shows long text as a stack of labels with a fixed number of lines each. A streaming reply only grows at its
    // end, so only the last label gets new text and the text engine does not regenerate the whole reply per delta.
    internal sealed class ChunkedText : VisualElement
    {
        public const int LinesPerChunk = 40;

        private readonly List<string> _chunks = new ();
        private readonly Func<string, string> _render;
        private readonly Func<Label> _createLabel;

        public ChunkedText(Func<string, string> render, Func<Label> createLabel)
        {
            _render = render;
            _createLabel = createLabel;
            AddToClassList("pl-md-chunks");
        }

        public static bool IsLong(string text)
        {
            return CountLines(text, LinesPerChunk + 1) > LinesPerChunk;
        }

        public void SetText(string text)
        {
            text ??= string.Empty;
            int chunk = 0;
            int start = 0;
            while (true)
            {
                int end = ChunkEnd(text, start);
                int length = end - start;
                if (chunk < _chunks.Count && _chunks[chunk].Length == length && string.CompareOrdinal(text, start, _chunks[chunk], 0, length) == 0)
                {
                    chunk++;
                }
                else
                {
                    string part = text.Substring(start, length);
                    if (chunk < _chunks.Count)
                    {
                        _chunks[chunk] = part;
                        ((Label)this[chunk]).text = _render(part);
                    }
                    else
                    {
                        _chunks.Add(part);
                        Label label = _createLabel();
                        label.text = _render(part);
                        Add(label);
                    }

                    chunk++;
                }

                if (end >= text.Length)
                {
                    break;
                }

                start = end + 1;
            }

            while (_chunks.Count > chunk)
            {
                _chunks.RemoveAt(_chunks.Count - 1);
                RemoveAt(childCount - 1);
            }
        }

        private static int ChunkEnd(string text, int start)
        {
            int lines = 1;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] != '\n')
                {
                    continue;
                }

                if (lines == LinesPerChunk)
                {
                    return i;
                }

                lines++;
            }

            return text.Length;
        }

        private static int CountLines(string text, int limit)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lines = 1;
            for (int i = 0; i < text.Length && lines < limit; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }
    }
}
