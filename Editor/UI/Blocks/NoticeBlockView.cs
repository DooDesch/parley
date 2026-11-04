using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class NoticeBlockView : BlockView
    {
        public NoticeBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-notice");
            AddToClassList("pl-notice--" + block.Level.ToString().ToLowerInvariant());
            ParleyIcon icon = block.Level == NoticeLevel.Error ? ParleyIcon.CircleAlert : block.Level == NoticeLevel.Warning ? ParleyIcon.TriangleAlert : ParleyIcon.Info;
            Add(ParleyIcons.Create(icon, "pl-notice__icon"));
            VisualElement content = new VisualElement();
            content.AddToClassList("pl-notice__content");
            Add(content);
            if (block.Level == NoticeLevel.Error)
            {
                Label label = ParleyStyles.Text(block.Text, "pl-notice__text");
                label.selection.isSelectable = true;
                ParleyStyles.UseMonospace(label);
                content.Add(label);
                return;
            }

            MarkdownView view = new MarkdownView();
            view.SetMarkdown(block.Text);
            content.Add(view);
        }

        public override void Refresh()
        {
        }
    }
}
