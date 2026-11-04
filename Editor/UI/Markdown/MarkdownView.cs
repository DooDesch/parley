using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class MarkdownView : VisualElement
    {
        private const long CopiedResetMs = 1200;
        private const float ListIndentWidth = 14.0f;

        private readonly List<MarkdownBlock> _blocks = new ();

        private string _source;
        private string _text;

        public MarkdownView()
        {
            AddToClassList("pl-md");
        }

        public static VisualElement CodeBlock(string code, string language, bool highlight = true)
        {
            CodeBlockContent content = new CodeBlockContent { Code = code, Language = language, Highlight = highlight };
            VisualElement container = new VisualElement { userData = content };
            container.AddToClassList("pl-md-code");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-md-code__header");
            header.Add(ParleyStyles.Text(string.IsNullOrEmpty(language) ? "code" : language == "error" ? "Error" : language, "pl-md-code__lang"));
            header.Add(ParleyStyles.Spacer());
            Button copy = ParleyIcons.IconButton(ParleyIcon.Copy, "Copy", null, "pl-md-code__copy");
            copy.tooltip = "Copy to clipboard";
            copy.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = content.Code;
                ParleyIcons.SetButton(copy, ParleyIcon.Check, "Copied");
                copy.schedule.Execute(() => ParleyIcons.SetButton(copy, ParleyIcon.Copy, "Copy")).ExecuteLater(CopiedResetMs);
            };

            header.Add(copy);
            container.Add(header);
            ScrollView scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.AddToClassList("pl-md-code__scroll");
            content.Body = new ChunkedText(content.Render, CreateCodeLabel);
            content.Body.AddToClassList("pl-md-code__body");
            content.Body.SetText(code);
            scroll.Add(content.Body);
            container.Add(scroll);
            return container;
        }

        public void SetMarkdown(string markdown)
        {
            if (markdown == _source)
            {
                return;
            }

            _source = markdown;
            string text = MarkdownParser.Normalize(markdown);

            // A streaming reply only grows at its end. Blocks before the last two cannot change then, so only the
            // tail is parsed again and the kept blocks stay as they are.
            int reused = 0;
            List<MarkdownBlock> parsed;
            if (_text != null && _blocks.Count > 2 && childCount == _blocks.Count && text.StartsWith(_text, StringComparison.Ordinal))
            {
                reused = _blocks.Count - 2;
                parsed = MarkdownParser.Parse(text, _blocks[reused].StartOffset);
            }
            else
            {
                parsed = MarkdownParser.Parse(text, 0);
            }

            _text = text;
            int total = reused + parsed.Count;
            int keep = reused;
            while (keep < total && keep < _blocks.Count && keep < childCount && parsed[keep - reused].ContentEquals(_blocks[keep]))
            {
                _blocks[keep] = parsed[keep - reused];
                keep++;
            }

            // While a reply streams, only its last block grows. Updating that element keeps its scroll position and
            // text selection and avoids rebuilding code blocks on every delta.
            if (keep < total && keep < _blocks.Count && keep < childCount && TryUpdateInPlace(this[keep], _blocks[keep], parsed[keep - reused]))
            {
                _blocks[keep] = parsed[keep - reused];
                keep++;
            }

            while (childCount > keep)
            {
                RemoveAt(childCount - 1);
            }

            if (_blocks.Count > keep)
            {
                _blocks.RemoveRange(keep, _blocks.Count - keep);
            }

            for (int i = keep; i < total; i++)
            {
                MarkdownBlock block = parsed[i - reused];
                Add(Build(block));
                _blocks.Add(block);
            }
        }

        private static Label CreateCodeLabel()
        {
            Label label = new Label { enableRichText = true };
            label.selection.isSelectable = true;
            label.AddToClassList("pl-md-code__text");
            ParleyStyles.UseMonospace(label);
            return label;
        }

        private static Label CreateParagraphLabel()
        {
            return ParleyStyles.RichLabel(string.Empty, "pl-md-p");
        }

        private static bool TryUpdateInPlace(VisualElement element, MarkdownBlock previous, MarkdownBlock next)
        {
            if (previous.Type != next.Type || previous.Level != next.Level || previous.Language != next.Language)
            {
                return false;
            }

            switch (next.Type)
            {
                case MarkdownBlockType.Code:
                    if (!(element.userData is CodeBlockContent content))
                    {
                        return false;
                    }

                    content.Code = next.Text;
                    content.Body.SetText(next.Text);
                    return true;
                case MarkdownBlockType.Heading:
                    if (!(element is Label heading))
                    {
                        return false;
                    }

                    heading.text = InlineMarkdown.ToRichText(next.Text);
                    return true;
                case MarkdownBlockType.Paragraph:
                    if (element is ChunkedText chunks)
                    {
                        chunks.SetText(next.Text);
                        return true;
                    }

                    if (!(element is Label label) || ChunkedText.IsLong(next.Text))
                    {
                        return false;
                    }

                    label.text = InlineMarkdown.ToRichText(next.Text);
                    return true;
                default:
                    return false;
            }
        }

        private static VisualElement Build(MarkdownBlock block)
        {
            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    return ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-h" + Mathf.Clamp(block.Level, 1, 4));
                case MarkdownBlockType.Code:
                    return CodeBlock(block.Text, block.Language);
                case MarkdownBlockType.Quote:
                    VisualElement quote = new VisualElement();
                    quote.AddToClassList("pl-md-quote");
                    quote.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-p"));
                    return quote;
                case MarkdownBlockType.List:
                    return BuildList(block);
                case MarkdownBlockType.Table:
                    return BuildTable(block);
                case MarkdownBlockType.Rule:
                    VisualElement rule = new VisualElement();
                    rule.AddToClassList("pl-md-rule");
                    return rule;
                default:
                    if (ChunkedText.IsLong(block.Text))
                    {
                        ChunkedText chunks = new ChunkedText(InlineMarkdown.ToRichText, CreateParagraphLabel);
                        chunks.AddToClassList("pl-md-p-chunks");
                        chunks.SetText(block.Text);
                        return chunks;
                    }

                    return ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-p");
            }
        }

        private static VisualElement BuildList(MarkdownBlock block)
        {
            VisualElement list = new VisualElement();
            list.AddToClassList("pl-md-list");
            foreach (MarkdownListItem item in block.Items)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("pl-md-list__item");
                row.style.marginLeft = item.Indent * ListIndentWidth;
                row.Add(Bullet(item));
                row.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(item.Text), "pl-md-list__text"));
                list.Add(row);
            }

            return list;
        }

        private static VisualElement Bullet(MarkdownListItem item)
        {
            if (item.Checked.HasValue)
            {
                VisualElement box = ParleyIcons.Create(item.Checked.Value ? ParleyIcon.SquareCheck : ParleyIcon.Square, "pl-md-list__check");
                box.EnableInClassList("pl-md-list__check--done", item.Checked.Value);
                return box;
            }

            if (char.IsDigit(item.Marker[0]))
            {
                return ParleyStyles.Text(item.Marker, "pl-md-list__bullet");
            }

            return ParleyStyles.Text(item.Indent % 2 == 0 ? "•" : "◦", "pl-md-list__bullet");
        }

        private static VisualElement BuildTable(MarkdownBlock block)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("pl-md-table");
            int columns = 0;
            foreach (string[] row in block.Rows)
            {
                columns = Mathf.Max(columns, row.Length);
            }

            for (int rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
            {
                string[] cells = block.Rows[rowIndex];
                VisualElement row = new VisualElement();
                row.AddToClassList("pl-md-table__row");
                row.EnableInClassList("pl-md-table__row--header", rowIndex == 0);
                for (int column = 0; column < columns; column++)
                {
                    string cell = column < cells.Length ? cells[column] : string.Empty;
                    row.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(cell), "pl-md-table__cell"));
                }

                scroll.Add(row);
            }

            return scroll;
        }

        private sealed class CodeBlockContent
        {
            public string Code;
            public string Language;
            public bool Highlight;
            public ChunkedText Body;

            public string Render(string part)
            {
                // Code labels keep whitespace; a tab would jump to TextCore's wide tab stop, so it becomes four spaces.
                if (part.IndexOf('\t') >= 0)
                {
                    part = part.Replace("\t", "    ");
                }

                return Highlight ? CodeHighlighter.Highlight(part, Language) : InlineMarkdown.Escape(part);
            }
        }
    }
}
