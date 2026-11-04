using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ToolCardView : BlockView
    {
        private readonly VisualElement _status;
        private readonly Label _title;
        private readonly Label _summary;
        private readonly VisualElement _chevron;
        private readonly VisualElement _body;
        private readonly VisualElement _nested;
        private readonly ParleyIcon _toolIcon;

        private bool _expanded;
        private bool _errorExpanded;
        private string _renderedSignature;
        private ParleyIcon? _shownIcon;

        public ToolCardView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-tool");
            _toolIcon = ToolPresenter.Icon(block.ToolName);
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-tool__header");
            _status = ParleyIcons.Create(_toolIcon, "pl-tool__status");
            _status.pickingMode = PickingMode.Position;
            _title = ParleyStyles.Text(string.Empty, "pl-tool__title");
            _summary = ParleyStyles.Text(string.Empty, "pl-tool__summary");
            _chevron = ParleyIcons.Create(ParleyIcon.ChevronRight, "pl-tool__chevron");
            header.Add(_status);
            header.Add(_title);
            header.Add(_summary);
            header.Add(_chevron);
            header.RegisterCallback<ClickEvent>(HeaderClickedHandler);
            header.tooltip = "Show or hide details";
            Add(header);
            _body = new VisualElement();
            _body.AddToClassList("pl-tool__body");
            Add(_body);
            _nested = new VisualElement();
            _nested.AddToClassList("pl-tool__children");
            ParleyStyles.SetVisible(_nested, false);
            Add(_nested);
            _expanded = ToolPresenter.ExpandedByDefault(block);
            RegisterCallback<DetachFromPanelEvent>(_ => ParleyIcons.SetSpinning(_status, false));
            RegisterCallback<AttachToPanelEvent>(_ => ParleyIcons.SetSpinning(_status, IsRunning));
            Refresh();
        }

        private bool IsRunning => !Block.IsError && Block.Result == null && !Block.IsFinished;

        public override void Refresh()
        {
            bool done = !Block.IsError && Block.Result != null;
            bool stopped = !Block.IsError && Block.Result == null && Block.IsFinished;
            bool running = IsRunning;
            ShowIcon(Block.IsError ? ParleyIcon.CircleX : running ? ParleyIcon.Loader : stopped ? ParleyIcon.CircleMinus : _toolIcon);
            ParleyIcons.SetSpinning(_status, running && panel != null);
            _status.tooltip = Block.IsError ? "Failed" : done ? "Done" : stopped ? "Stopped before it finished" : "Running";
            _status.EnableInClassList("pl-tool__status--running", running);
            _status.EnableInClassList("pl-tool__status--stopped", stopped);
            _status.EnableInClassList("pl-tool__status--done", done);
            _status.EnableInClassList("pl-tool__status--error", Block.IsError);
            EnableInClassList("pl-tool--running", running);
            EnableInClassList("pl-tool--error", Block.IsError);
            _title.text = ToolPresenter.DisplayName(Block.ToolName);
            _title.tooltip = Block.ToolName;
            _summary.text = ToolPresenter.Summary(Block);
            _summary.tooltip = _summary.text;
            if (Block.IsError && !_errorExpanded)
            {
                _errorExpanded = true;
                _expanded = true;
            }

            EnableInClassList("pl-tool--expanded", _expanded);
            ParleyIcons.Set(_chevron, _expanded ? ParleyIcon.ChevronDown : ParleyIcon.ChevronRight);
            ParleyStyles.SetVisible(_body, _expanded);
            if (!_expanded)
            {
                return;
            }

            string signature = Block.IsFinished + "|" + (Block.Result?.Length ?? -1) + "|" + (Block.Input?.Count ?? -1) + "|" + Block.IsError
                + "|" + (Block.PartialInputJson?.Length ?? -1);
            if (signature == _renderedSignature)
            {
                return;
            }

            _renderedSignature = signature;
            _body.Clear();
            if (Block.Input == null)
            {
                _body.Add(ParleyStyles.Text(Block.IsFinished ? "No parameters." : "Preparing…", ParleyStyles.Muted));
                return;
            }

            _body.Add(ToolPresenter.BuildBody(Block));
        }

        public void AddNested(VisualElement view)
        {
            _nested.Add(view);
            ParleyStyles.SetVisible(_nested, true);
        }

        private void ShowIcon(ParleyIcon icon)
        {
            if (_shownIcon == icon)
            {
                return;
            }

            _shownIcon = icon;
            ParleyIcons.Set(_status, icon);
        }

        private void HeaderClickedHandler(ClickEvent evt)
        {
            _expanded = !_expanded;
            _renderedSignature = null;
            Refresh();
        }
    }
}
