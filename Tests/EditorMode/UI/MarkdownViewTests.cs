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
        public void LongStreamingParagraphOnlyUpdatesItsLastChunk()
        {
            MarkdownView view = new MarkdownView();
            string text = "Intro\n\n" + Lines("line", 90);
            view.SetMarkdown(text);
            VisualElement chunks = view[1];
            Label first = (Label)chunks[0];
            string firstText = first.text;

            view.SetMarkdown(text + " grows");

            Assert.AreSame(chunks, view[1]);
            Assert.AreEqual(3, chunks.childCount);
            Assert.AreSame(first, chunks[0]);
            Assert.AreEqual(firstText, first.text);
            StringAssert.Contains("grows", ((Label)chunks[2]).text);
        }

        [Test]
        public void IncrementalParseMatchesFullParse()
        {
            string full = "# Title\n\nSome text\n\n- a\n- b\n\n| x | y |\n|---|---|\n| 1 | 2 |\n\n```cs\nint a;\n```\n\nTail paragraph\nmore";
            MarkdownView view = new MarkdownView();
            for (int length = 1; length <= full.Length; length += 3)
            {
                view.SetMarkdown(full.Substring(0, length));
            }

            view.SetMarkdown(full);
            MarkdownView fresh = new MarkdownView();
            fresh.SetMarkdown(full);

            Assert.AreEqual(fresh.childCount, view.childCount);
            for (int i = 0; i < fresh.childCount; i++)
            {
                Assert.AreEqual(fresh[i].GetType(), view[i].GetType());
                CollectionAssert.AreEqual(fresh[i].GetClasses(), view[i].GetClasses());
            }
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

        private static string Lines(string prefix, int count)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(prefix).Append(' ').Append(i);
            }

            return builder.ToString();
        }
    }
}
