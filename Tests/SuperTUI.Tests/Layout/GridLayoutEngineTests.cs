using System;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using SuperTUI.Core;
using Xunit;

namespace SuperTUI.Tests.Layout
{
    public class GridLayoutEngineTests
    {
        [Fact]
        public void Constructor_CreatesGridWithCorrectRowsAndColumns()
        {
            // Arrange & Act
            var layout = new GridLayoutEngine(3, 4, enableSplitters: false);

            // Assert
            var grid = layout.Container as Grid;
            grid.Should().NotBeNull();
            grid.RowDefinitions.Should().HaveCount(3);
            grid.ColumnDefinitions.Should().HaveCount(4);
        }

        [Fact]
        public void Constructor_WithSplitters_AddsVerticalSplitters()
        {
            // Arrange & Act
            var layout = new GridLayoutEngine(2, 3, enableSplitters: true);
            var grid = layout.Container as Grid;

            // Assert - Should have 2 vertical splitters (between 3 columns)
            var splitters = grid.Children.OfType<GridSplitter>()
                .Where(s => s.ResizeDirection == GridResizeDirection.Columns)
                .ToList();
            splitters.Should().HaveCount(2);
        }

        [Fact]
        public void Constructor_WithSplitters_AddsHorizontalSplitters()
        {
            // Arrange & Act
            var layout = new GridLayoutEngine(3, 2, enableSplitters: true);
            var grid = layout.Container as Grid;

            // Assert - Should have 2 horizontal splitters (between 3 rows)
            var splitters = grid.Children.OfType<GridSplitter>()
                .Where(s => s.ResizeDirection == GridResizeDirection.Rows)
                .ToList();
            splitters.Should().HaveCount(2);
        }

        [Fact]
        public void AddChild_WithValidRowAndColumn_AddsToGrid()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 0, Column = 1 };

            // Act
            layout.AddChild(button, layoutParams);

            // Assert
            var grid = layout.Container as Grid;
            grid.Children.Should().Contain(button);
            Grid.GetRow(button).Should().Be(0);
            Grid.GetColumn(button).Should().Be(1);
        }

        [Fact]
        public void AddChild_WithInvalidRow_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 5, Column = 0 }; // Row 5 doesn't exist

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, layoutParams));
            exception.ParamName.Should().Be("Row");
            exception.Message.Should().Contain("Row 5 is invalid");
        }

        [Fact]
        public void AddChild_WithInvalidColumn_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 0, Column = 10 }; // Column 10 doesn't exist

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, layoutParams));
            exception.ParamName.Should().Be("Column");
            exception.Message.Should().Contain("Column 10 is invalid");
        }

        [Fact]
        public void AddChild_WithColumnSpanExceedingBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 3, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 0, Column = 1, ColumnSpan = 5 }; // Span too large

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, layoutParams));
            exception.ParamName.Should().Be("ColumnSpan");
        }

        [Fact]
        public void AddChild_WithRowSpanExceedingBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 3, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 1, Column = 0, RowSpan = 5 }; // Span too large

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, layoutParams));
            exception.ParamName.Should().Be("RowSpan");
        }

        [Fact]
        public void AddChild_WithValidSpan_SetsSpanCorrectly()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 3, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams
            {
                Row = 0,
                Column = 0,
                RowSpan = 2,
                ColumnSpan = 2
            };

            // Act
            layout.AddChild(button, layoutParams);

            // Assert
            Grid.GetRowSpan(button).Should().Be(2);
            Grid.GetColumnSpan(button).Should().Be(2);
        }

        [Fact]
        public void RemoveChild_RemovesFromGrid()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2, enableSplitters: false);
            var button = new Button();
            var layoutParams = new LayoutParams { Row = 0, Column = 0 };
            layout.AddChild(button, layoutParams);

            // Act
            layout.RemoveChild(button);

            // Assert
            var grid = layout.Container as Grid;
            grid.Children.Should().NotContain(button);
        }

        [Fact]
        public void Clear_RemovesAllChildren()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2, enableSplitters: false);
            layout.AddChild(new Button(), new LayoutParams { Row = 0, Column = 0 });
            layout.AddChild(new Button(), new LayoutParams { Row = 0, Column = 1 });
            layout.AddChild(new Button(), new LayoutParams { Row = 1, Column = 0 });

            // Act
            layout.Clear();

            // Assert
            var grid = layout.Container as Grid;
            grid.Children.Should().BeEmpty();
        }

        [Fact]
        public void SetColumnWidth_SetsWidthCorrectly()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 3, enableSplitters: false);
            var grid = layout.Container as Grid;

            // Act
            layout.SetColumnWidth(1, new GridLength(200, GridUnitType.Pixel));

            // Assert
            grid.ColumnDefinitions[1].Width.Value.Should().Be(200);
            grid.ColumnDefinitions[1].Width.GridUnitType.Should().Be(GridUnitType.Pixel);
        }

        [Fact]
        public void SetRowHeight_SetsHeightCorrectly()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 2, enableSplitters: false);
            var grid = layout.Container as Grid;

            // Act
            layout.SetRowHeight(1, new GridLength(2, GridUnitType.Star));

            // Assert
            grid.RowDefinitions[1].Height.Value.Should().Be(2);
            grid.RowDefinitions[1].Height.GridUnitType.Should().Be(GridUnitType.Star);
        }
    }
}
