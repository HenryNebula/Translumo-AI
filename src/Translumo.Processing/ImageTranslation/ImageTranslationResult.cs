using System.Collections.Generic;
using System.Drawing;

namespace Translumo.Processing.ImageTranslation
{
    /// <summary>One OCR line with its translation and pixel bounding box (image-relative).</summary>
    public sealed class TranslatedLine
    {
        public string Source { get; init; }

        public string Translation { get; init; }

        public RectangleF Box { get; init; }
    }

    /// <summary>Full result of translating a captured region (Google Lens style).</summary>
    public sealed class ImageTranslationResult
    {
        public IReadOnlyList<TranslatedLine> Lines { get; init; } = new List<TranslatedLine>();

        /// <summary>BCP-47 tag of the recognizer used (e.g. "en-US"); null when nothing was detected.</summary>
        public string DetectedLanguageTag { get; init; }

        public int ImageWidth { get; init; }

        public int ImageHeight { get; init; }

        /// <summary>
        /// True when the result was produced by a one-pass vision model (no per-line OCR boxes).
        /// In that case <see cref="FullText"/> holds the whole translation and <see cref="Lines"/>
        /// is empty; the overlay renders a single text block instead of positioned boxes.
        /// </summary>
        public bool IsTextOnly { get; init; }

        /// <summary>Whole translation text for a vision one-pass (<see cref="IsTextOnly"/>) result.</summary>
        public string FullText { get; init; }

        public bool HasText => IsTextOnly ? !string.IsNullOrWhiteSpace(FullText) : Lines.Count > 0;

        /// <summary>Builds a vision one-pass (text-only) result.</summary>
        public static ImageTranslationResult TextOnly(string text) => new ImageTranslationResult
        {
            IsTextOnly = true,
            FullText = text ?? string.Empty
        };
    }
}
