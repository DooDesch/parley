using System.Collections.Generic;
using System.Text;

namespace DTech.Parley.Editor.UI
{
    internal sealed class MarkdownBlock
    {
        public List<MarkdownListItem> Items { get; } = new ();
        public List<string[]> Rows { get; } = new ();
        public MarkdownBlockType Type { get; set; }
        public int Level { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; }
        public bool IsClosed { get; set; } = true;

        // Character offset of the block's first line in the normalized source, so a growing reply can be re-parsed
        // from the last blocks only.
        public int StartOffset { get; set; }

        public string Signature
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                builder.Append((int)Type).Append('|').Append(Level).Append('|').Append(Language).Append('|').Append(IsClosed).Append('|').Append(Text);
                foreach (MarkdownListItem item in Items)
                {
                    builder.Append('\u0001').Append(item.Indent).Append(item.Marker).Append(item.Checked).Append(item.Text);
                }

                foreach (string[] row in Rows)
                {
                    builder.Append('\u0002').Append(string.Join("\u0003", row));
                }

                return builder.ToString();
            }
        }

        public bool ContentEquals(MarkdownBlock other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || Type != other.Type || Level != other.Level || Language != other.Language || IsClosed != other.IsClosed
                || Text != other.Text || Items.Count != other.Items.Count || Rows.Count != other.Rows.Count)
            {
                return false;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                MarkdownListItem a = Items[i];
                MarkdownListItem b = other.Items[i];
                if (a.Indent != b.Indent || a.Marker != b.Marker || a.Checked != b.Checked || a.Text != b.Text)
                {
                    return false;
                }
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                string[] a = Rows[i];
                string[] b = other.Rows[i];
                if (a.Length != b.Length)
                {
                    return false;
                }

                for (int cell = 0; cell < a.Length; cell++)
                {
                    if (a[cell] != b[cell])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
