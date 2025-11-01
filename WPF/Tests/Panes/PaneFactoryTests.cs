using System;
using System.Linq;
using FluentAssertions;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Panes
{
    /// <summary>
    /// Tests for PaneFactory - verifies all panes can be created via DI
    /// CRITICAL: This validates the entire pane creation pipeline
    /// </summary>
    [Trait("Category", "Linux")]
    [Trait("Category", "Critical")]
    [Trait("Priority", "Critical")]
    public class PaneFactoryTests : PaneTestBase
    {
        [WpfTheory]
        [InlineData("tasks")]
        [InlineData("notes")]
        [InlineData("files")]
        [InlineData("projects")]
        [InlineData("help")]
        [InlineData("calendar")]
        public void CreatePane_AllRegisteredPanes_ShouldInstantiate(string paneType)
        {
            // Act
            Action act = () => PaneFactory.CreatePane(paneType);

            // Assert
            act.Should().NotThrow($"{paneType} pane should instantiate via DI");
        }

        [WpfFact]
        public void CreatePane_AllRegisteredPanes_ShouldReturnNonNullPanes()
        {
            // Arrange
            var paneTypes = new[] { "tasks", "notes", "files", "projects", "help", "calendar" };

            // Act & Assert
            foreach (var paneType in paneTypes)
            {
                var pane = PaneFactory.CreatePane(paneType);
                pane.Should().NotBeNull($"{paneType} pane should be created");
                pane.PaneName.Should().NotBeNullOrEmpty($"{paneType} pane should have a name");
                pane.Should().BeAssignableTo<PaneBase>($"{paneType} should inherit from PaneBase");
            }
        }

        [WpfFact]
        public void CreatePane_UnknownType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => PaneFactory.CreatePane("NonExistentPane");

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unknown pane type*");
        }

        [WpfFact]
        public void CreatePane_NullType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => PaneFactory.CreatePane(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [WpfFact]
        public void CreatePane_EmptyType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => PaneFactory.CreatePane("");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [WpfFact]
        public void CreatePane_WhitespaceType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => PaneFactory.CreatePane("   ");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [WpfFact]
        public void CreatePane_CaseInsensitive_ShouldWork()
        {
            // Act
            var pane1 = PaneFactory.CreatePane("tasks");
            var pane2 = PaneFactory.CreatePane("TASKS");
            var pane3 = PaneFactory.CreatePane("Tasks");

            // Assert
            pane1.Should().NotBeNull();
            pane2.Should().NotBeNull();
            pane3.Should().NotBeNull();
        }

        [WpfFact]
        public void GetAvailablePaneTypes_ShouldReturnAllTypes()
        {
            // Act
            var paneTypes = PaneFactory.GetAvailablePaneTypes().ToList();

            // Assert
            paneTypes.Should().NotBeEmpty();
            paneTypes.Should().Contain("tasks");
            paneTypes.Should().Contain("notes");
            paneTypes.Should().Contain("files");
            paneTypes.Should().Contain("projects");
            paneTypes.Should().Contain("help");
            paneTypes.Should().Contain("calendar");
            paneTypes.Should().HaveCount(6, "excel-import is currently disabled in PaneFactory");
        }

        [WpfFact]
        public void GetPaletteVisiblePaneTypes_ShouldExcludeHiddenPanes()
        {
            // Act
            var visiblePaneTypes = PaneFactory.GetPaletteVisiblePaneTypes().ToList();

            // Assert
            visiblePaneTypes.Should().NotContain("files", "files pane is hidden from palette");
            visiblePaneTypes.Should().Contain("tasks");
            visiblePaneTypes.Should().Contain("notes");
        }

        [WpfFact]
        public void GetPaneMetadata_ForValidPane_ShouldReturnMetadata()
        {
            // Act
            var metadata = PaneFactory.GetPaneMetadata("tasks");

            // Assert
            metadata.Should().NotBeNull();
            metadata.Name.Should().Be("tasks");
            metadata.Description.Should().NotBeNullOrEmpty();
            metadata.Icon.Should().NotBeNullOrEmpty();
            metadata.Creator.Should().NotBeNull();
        }

        [WpfFact]
        public void GetPaneMetadata_ForInvalidPane_ShouldReturnNull()
        {
            // Act
            var metadata = PaneFactory.GetPaneMetadata("invalid");

            // Assert
            metadata.Should().BeNull();
        }

        [WpfFact]
        public void GetAllPaneMetadata_ShouldReturnAllMetadata()
        {
            // Act
            var allMetadata = PaneFactory.GetAllPaneMetadata().ToList();

            // Assert
            allMetadata.Should().HaveCount(6, "excel-import is currently disabled in PaneFactory");
            allMetadata.Should().OnlyContain(m => m.Name != null);
            allMetadata.Should().OnlyContain(m => m.Description != null);
            allMetadata.Should().OnlyContain(m => m.Creator != null);
        }

        [WpfFact]
        public void HasPaneType_ForValidPane_ShouldReturnTrue()
        {
            // Act
            var exists = PaneFactory.HasPaneType("tasks");

            // Assert
            exists.Should().BeTrue();
        }

        [WpfFact]
        public void HasPaneType_ForInvalidPane_ShouldReturnFalse()
        {
            // Act
            var exists = PaneFactory.HasPaneType("invalid");

            // Assert
            exists.Should().BeFalse();
        }

        [WpfFact]
        public void CreatePane_Disposal_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow("Pane disposal should be clean");
        }

        [WpfFact]
        public void CreatePane_MultipleDisposal_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("notes");

            // Act
            pane.Dispose();
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow("Double disposal should be safe");
        }

        [WpfFact]
        public void CreatePane_MultiplePanes_AllShouldDispose()
        {
            // Arrange
            var panes = new[]
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes"),
                PaneFactory.CreatePane("projects")
            };

            // Act & Assert
            foreach (var pane in panes)
            {
                Action act = () => pane.Dispose();
                act.Should().NotThrow($"{pane.PaneName} should dispose cleanly");
            }
        }

        [WpfFact]
        public void RegisterPaneType_CustomPane_ShouldSucceed()
        {
            // Arrange
            var customPaneCreated = false;
            PaneBase CustomPaneCreator()
            {
                customPaneCreated = true;
                return PaneFactory.CreatePane("tasks"); // Use existing pane for simplicity
            }

            // Act
            PaneFactory.RegisterPaneType("custom", "Custom pane", "🔧", CustomPaneCreator);
            var pane = PaneFactory.CreatePane("custom");

            // Assert
            customPaneCreated.Should().BeTrue();
            pane.Should().NotBeNull();
        }

        [WpfFact]
        public void RegisterPaneType_NullCreator_ShouldThrow()
        {
            // Act
            Action act = () => PaneFactory.RegisterPaneType("custom", "Custom", "🔧", null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [WpfFact]
        public void RegisterPaneType_EmptyName_ShouldThrow()
        {
            // Act
            Action act = () => PaneFactory.RegisterPaneType("", "Custom", "🔧", () => null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
