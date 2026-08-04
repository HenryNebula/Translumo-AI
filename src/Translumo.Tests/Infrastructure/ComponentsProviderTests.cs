using System.IO;
using Translumo.Infrastructure.Components;
using Translumo.Infrastructure.Constants;
using Xunit;

namespace Translumo.Tests.Infrastructure
{
    public class ComponentsProviderTests
    {
        [Theory]
        [InlineData(ComponentsProvider.ComponentKind.Python, "python")]
        [InlineData(ComponentsProvider.ComponentKind.EasyOcr, "models/easyocr")]
        [InlineData(ComponentsProvider.ComponentKind.Tessdata, "models/tessdata")]
        public void GetComponentPaths_returns_expected_zip_entry(ComponentsProvider.ComponentKind kind, string expectedEntry)
        {
            var (zipEntry, _) = ComponentsProvider.GetComponentPaths(kind);
            Assert.Equal(expectedEntry, zipEntry);
        }

        [Fact]
        public void GetComponentPaths_targets_app_model_dirs()
        {
            Assert.Equal(Global.PythonPath,
                ComponentsProvider.GetComponentPaths(ComponentsProvider.ComponentKind.Python).TargetDirectory);
            Assert.Equal(Path.Combine(Global.ModelsPath, "easyocr"),
                ComponentsProvider.GetComponentPaths(ComponentsProvider.ComponentKind.EasyOcr).TargetDirectory);
            Assert.Equal(Path.Combine(Global.ModelsPath, "tessdata"),
                ComponentsProvider.GetComponentPaths(ComponentsProvider.ComponentKind.Tessdata).TargetDirectory);
        }

        [Fact]
        public void IsComponentPresent_false_when_target_not_populated()
        {
            Assert.False(ComponentsProvider.IsComponentPresent(ComponentsProvider.ComponentKind.Tessdata));
        }
    }
}
