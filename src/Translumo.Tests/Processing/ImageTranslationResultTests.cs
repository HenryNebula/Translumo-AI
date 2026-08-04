using System.Collections.Generic;
using Translumo.Processing.ImageTranslation;
using Xunit;

namespace Translumo.Tests.Processing
{
    public class ImageTranslationResultTests
    {
        [Fact]
        public void TextOnly_sets_IsTextOnly_and_FullText()
        {
            var result = ImageTranslationResult.TextOnly("hello");

            Assert.True(result.IsTextOnly);
            Assert.Equal("hello", result.FullText);
            Assert.Empty(result.Lines);
        }

        [Fact]
        public void TextOnly_null_text_becomes_empty()
        {
            Assert.Equal(string.Empty, ImageTranslationResult.TextOnly(null).FullText);
        }

        [Fact]
        public void HasText_true_for_non_empty_text_only()
        {
            Assert.True(ImageTranslationResult.TextOnly("hi").HasText);
        }

        [Fact]
        public void HasText_false_for_empty_text_only()
        {
            Assert.False(ImageTranslationResult.TextOnly("").HasText);
            Assert.False(ImageTranslationResult.TextOnly("   ").HasText);
        }

        [Fact]
        public void HasText_uses_lines_for_positional_result()
        {
            var withLine = new ImageTranslationResult
            {
                Lines = new List<TranslatedLine> { new TranslatedLine { Translation = "x" } }
            };
            Assert.True(withLine.HasText);
            Assert.False(withLine.IsTextOnly);

            var empty = new ImageTranslationResult();
            Assert.False(empty.HasText);
            Assert.False(empty.IsTextOnly);
        }
    }
}
