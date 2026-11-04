using DTech.Parley.Editor.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class MarkdownViewTests
    {
        [Test]
        public void GrowingParagraphKeepsItsElement()
        {
            MarkdownView view = new MarkdownView();
            view.SetMarkdown("First paragraph.\n\nSecond");
            VisualElement first = view[0];
            VisualElement streaming = view[1];

            view.SetMarkdown("First paragraph.\n\nSecond paragraph grows");

            Assert.AreEqual(2, view.childCount);
            Assert.AreSame(first, view[0]);
            Assert.AreSame(streaming, view[1]);
            StringAssert.Contains("grows", ((Label)view[1]).text);
        }

        [Test]
        public void GrowingCodeBlockKeepsItsElement()
        {
            MarkdownView view = new MarkdownView();
            view.SetMarkdown("```csharp\nint a = 1;");
            VisualElement code = view[0];

            view.SetMarkdown("```csharp\nint a = 1;\nint b = 2;\n```");

            Assert.AreEqual(1, view.childCount);
            Assert.AreSame(code, view[0]);
            StringAssert.Contains("b", code.Q<Label>(className: "pl-md-code__text").text);
        }

        [Test]
        public void ChangedBlockTypeIsRebuilt()
        {
            MarkdownView view = new MarkdownView();
            view.SetMarkdown("Intro\n\n#");
            VisualElement before = view[1];

            view.SetMarkdown("Intro\n\n# Title");

            Assert.AreEqual(2, view.childCount);
            Assert.AreNotSame(before, view[1]);
        }
    }
}
