using System;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ThinkingBlockView : BlockView
    {
        private const string EmptyClass = "pl-block--thinking-empty";

        private readonly Foldout _foldout = new () { value = false };
        private readonly Label _text;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private readonly bool _fromHistory;

        private int _seconds = -1;

        public ThinkingBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-block--thinking");
            _fromHistory = block.IsFinished;
            _text = ParleyStyles.Text(string.Empty, "pl-thinking__text");
            _text.selection.isSelectable = true;
            _foldout.Add(_text);
            Add(_foldout);
            Refresh();
        }

        public override void Refresh()
        {
            if (!Block.IsFinished)
            {
                _foldout.text = "Thinking… " + (int)(DateTime.UtcNow - _startedUtc).TotalSeconds + "s";
            }
            else
            {
                // The duration is only known for a block that finished while this view watched it.
                if (_seconds < 0 && !_fromHistory)
                {
                    _seconds = Math.Max(1, (int)Math.Round((DateTime.UtcNow - _startedUtc).TotalSeconds));
                }

                _foldout.text = _seconds > 0 ? "Thought for " + _seconds + "s" : "Thought";
            }

            // Models with hidden reasoning send a thinking block without text; it shows as a plain line then.
            bool empty = Block.IsFinished && string.IsNullOrEmpty(Block.Text);
            EnableInClassList(EmptyClass, empty);
            _foldout.tooltip = empty ? "The model did not send its thinking text." : null;
            if (empty && _foldout.value)
            {
                _foldout.value = false;
            }

            if (_foldout.value || Block.IsFinished)
            {
                _text.text = Block.Text;
            }
        }
    }
}
