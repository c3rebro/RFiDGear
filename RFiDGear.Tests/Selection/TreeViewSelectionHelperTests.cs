using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Helpers.Selection;

namespace RFiDGear.Tests.Selection
{
    [TestClass]
    public class TreeViewSelectionHelperTests
    {
        [TestMethod]
        public void ClearSelection_ClearsNestedSelections()
        {
            var grandChild = new TestNode { IsSelected = true };
            var child = new TestNode { IsSelected = true, SelectionChildren = new List<ITreeSelectionNode> { grandChild } };
            var parent = new TestNode { IsSelected = true, SelectionChildren = new List<ITreeSelectionNode> { child } };

            TreeViewSelectionHelper.ClearSelection(new[] { parent }, null);

            Assert.IsFalse(parent.IsSelected);
            Assert.IsFalse(child.IsSelected);
            Assert.IsFalse(grandChild.IsSelected);
        }

        [TestMethod]
        public void ClearSelection_IgnoresNullCollections()
        {
            var parent = new TestNode { IsSelected = true, SelectionChildren = null };

            TreeViewSelectionHelper.ClearSelection(new[] { parent }, null);

            Assert.IsFalse(parent.IsSelected);
        }

        private class TestNode : ITreeSelectionNode
        {
            public bool IsSelected { get; set; }

            public IEnumerable<ITreeSelectionNode> SelectionChildren { get; set; } = new List<ITreeSelectionNode>();
        }
    }
}
