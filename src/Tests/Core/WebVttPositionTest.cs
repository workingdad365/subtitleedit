using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Tests.Core
{
    [TestClass]
    public class WebVttPositionTest
    {
        [TestMethod]
        [DataRow("74.00", "")]
        [DataRow("79.33", "")]
        [DataRow("84.67", "")]
        [DataRow("66.67", "")]
        [DataRow("66.66", "{\\an5}")]
        [DataRow("50.00", "{\\an5}")]
        [DataRow("10.00", "{\\an8}")]
        public void LoadSubtitleMapsVerticalPosition(string line, string expectedAlignment)
        {
            var subtitle = new Subtitle();
            var lines = new List<string>
            {
                "WEBVTT",
                "",
                "00:11:00.326 --> 00:11:02.578 position:50.00%,middle align:middle size:80.00% line:" + line + "%",
                "First line",
                "Second line",
                "Third line",
                "",
            };

            new WebVTT().LoadSubtitle(subtitle, lines, null);

            Assert.AreEqual(1, subtitle.Paragraphs.Count);
            var paragraph = subtitle.Paragraphs[0];
            Assert.AreEqual(expectedAlignment + "First line\nSecond line\nThird line",
                paragraph.Text.Replace("\r\n", "\n"));
            StringAssert.Contains(paragraph.Style, "line:" + line + "%");
            StringAssert.Contains(new SubRip().ToText(subtitle, string.Empty),
                expectedAlignment + "First line");
        }

        [TestMethod]
        public void HorizontalMiddleDoesNotImplyVerticalMiddle()
        {
            var subtitle = new Subtitle();
            new WebVTT().LoadSubtitle(subtitle, new List<string>
            {
                "WEBVTT",
                "",
                "00:00:01.000 --> 00:00:02.000 position:50.00%,middle align:middle",
                "Bottom subtitle",
                "",
            }, null);

            Assert.AreEqual("Bottom subtitle", subtitle.Paragraphs[0].Text);
        }
    }
}
