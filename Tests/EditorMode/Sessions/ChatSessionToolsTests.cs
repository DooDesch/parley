using DTech.Parley.Editor.Sessions;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ChatSessionToolsTests
    {
        [TestCase("Read", true)]
        [TestCase("Grep", true)]
        [TestCase("TodoWrite", true)]
        [TestCase("ExitPlanMode", true)]
        [TestCase("Write", false)]
        [TestCase("Edit", false)]
        [TestCase("Bash", false)]
        [TestCase("mcp__unity__refresh", false)]
        [TestCase("SomeFutureTool", false)]
        [TestCase(null, false)]
        public void OnlyKnownReadOnlyToolsSkipTheAssetRefresh(string toolName, bool readOnly)
        {
            Assert.AreEqual(readOnly, ChatSession.IsReadOnlyTool(toolName));
        }
    }
}
