using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Translumo.Translation.Llm;
using Xunit;

namespace Translumo.Tests.Llm
{
    /// <summary>
    /// White-box tests for the (private) vision request builders in LlmTranslator. They validate
    /// that each vendor's multimodal payload (OpenAI image_url, Anthropic image block, Gemini
    /// inline_data) is well-formed and that the PNG bytes survive the base64 round-trip.
    /// Mirrors <see cref="LlmTranslatorRequestTests"/> but the vision builders take a byte[] image.
    /// </summary>
    public class LlmTranslatorVisionRequestTests
    {
        // Arbitrary bytes (PNG header-ish); the builders only base64-encode whatever they get, so the
        // tests assert a faithful round-trip rather than a real image.
        private static readonly byte[] Image = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static string InvokeBuildVision(string method, LlmConfiguration cfg, string system, byte[] image)
        {
            var m = typeof(LlmTranslator).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            return (string)m!.Invoke(null, new object[] { cfg, system, image })!;
        }

        [Fact]
        public void OpenAi_vision_request_has_system_text_and_png_image_url()
        {
            var cfg = new LlmConfiguration { Provider = LlmProvider.ChatGPT };
            var json = InvokeBuildVision("BuildOpenAiVisionRequest", cfg, "SYS", Image);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());

            var messages = root.GetProperty("messages").EnumerateArray().ToArray();
            Assert.Equal(2, messages.Length);
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal("SYS", messages[0].GetProperty("content").GetString());

            // User turn carries a text part then an image_url part whose data URL embeds the PNG.
            var userContent = messages[1].GetProperty("content").EnumerateArray().ToArray();
            Assert.Equal("text", userContent[0].GetProperty("type").GetString());
            Assert.Equal("image_url", userContent[1].GetProperty("type").GetString());

            var url = userContent[1].GetProperty("image_url").GetProperty("url").GetString()!;
            Assert.StartsWith("data:image/png;base64,", url);
            var b64 = url.Substring("data:image/png;base64,".Length);
            Assert.Equal(Image, Convert.FromBase64String(b64));

            Assert.True(root.TryGetProperty("temperature", out _));
            Assert.True(root.TryGetProperty("max_tokens", out _));
        }

        [Fact]
        public void Anthropic_vision_request_has_image_block_text_block_and_system()
        {
            var cfg = new LlmConfiguration { Provider = LlmProvider.Claude };
            var json = InvokeBuildVision("BuildAnthropicVisionRequest", cfg, "SYS", Image);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("claude-sonnet-4-6", root.GetProperty("model").GetString());
            Assert.Equal("SYS", root.GetProperty("system").GetString());

            var content = root.GetProperty("messages").EnumerateArray().First()
                              .GetProperty("content").EnumerateArray().ToArray();
            Assert.Equal(2, content.Length);

            // Image block carries the base64 PNG source.
            var imageBlock = content[0];
            Assert.Equal("image", imageBlock.GetProperty("type").GetString());
            var source = imageBlock.GetProperty("source");
            Assert.Equal("base64", source.GetProperty("type").GetString());
            Assert.Equal("image/png", source.GetProperty("media_type").GetString());
            Assert.Equal(Image, Convert.FromBase64String(source.GetProperty("data").GetString()!));

            Assert.Equal("text", content[1].GetProperty("type").GetString());
        }

        [Fact]
        public void Gemini_vision_request_has_inline_data_and_system_instruction()
        {
            var cfg = new LlmConfiguration { Provider = LlmProvider.Gemini };
            var json = InvokeBuildVision("BuildGeminiVisionRequest", cfg, "SYS", Image);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var parts = root.GetProperty("contents").EnumerateArray().First()
                            .GetProperty("parts").EnumerateArray().ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal("Translate all visible text in this image.", parts[0].GetProperty("text").GetString());

            var inline = parts[1].GetProperty("inline_data");
            Assert.Equal("image/png", inline.GetProperty("mime_type").GetString());
            Assert.Equal(Image, Convert.FromBase64String(inline.GetProperty("data").GetString()!));

            Assert.Equal("SYS",
                root.GetProperty("systemInstruction").GetProperty("parts").EnumerateArray().First()
                    .GetProperty("text").GetString());
            Assert.True(root.GetProperty("generationConfig").TryGetProperty("maxOutputTokens", out _));
        }
    }
}
