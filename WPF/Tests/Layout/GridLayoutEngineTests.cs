using System;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using SuperTUI.Layout;

namespace SuperTUI.Tests.Layout
{
    public class GridLayoutEngineTests
    {
        [Fact]
        public void Constructor_ShouldCreateGridWithSpecifiedDimensions()
        {
            // Act
            var layout = new GridLayoutEngine(3, 4);

            // Assert
            var grid = layout.Container as Grid;
            Assert.NotNull(grid);
            Assert.Equal(3, grid.RowDefinitions.Count);
            Assert.Equal(4, grid.ColumnDefinitions.Count);
        }

        [Fact]
        public void AddRows_ShouldAddRowDefinitions()
        {
            // Arrange
            var layout = new GridLayoutEngine(1, 1);

            // Act
            layout.AddRows(SizeMode.Auto, SizeMode.Star, SizeMode.Pixels);

            // Assert
            var grid = layout.Container as Grid;
            Assert.Equal(4, grid.RowDefinitions.Count); // 1 original + 3 added
            Assert.Equal(GridLength.Auto, grid.RowDefinitions[1].Height);
            Assert.Equal(new GridLength(1, GridUnitType.Star), grid.RowDefinitions[2].Height);
            Assert.Equal(GridLength.Auto, grid.RowDefinitions[3].Height); // Pixels without value defaults to Auto
        }

        [Fact]
        public void AddColumns_ShouldAddColumnDefinitions()
        {
            // Arrange
            var layout = new GridLayoutEngine(1, 1);

            // Act
            layout.AddColumns(SizeMode.Star, SizeMode.Auto);

            // Assert
            var grid = layout.Container as Grid;
            Assert.Equal(3, grid.ColumnDefinitions.Count); // 1 original + 2 added
            Assert.Equal(new GridLength(1, GridUnitType.Star), grid.ColumnDefinitions[1].Width);
            Assert.Equal(GridLength.Auto, grid.ColumnDefinitions[2].Width);
        }

        [Fact]
        public void AddChild_WithValidRowColumn_ShouldPlaceElement()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 3);
            var button = new Button { Content = "Test" };

            // Act
            layout.AddChild(button, new LayoutParams { Row = 1, Column = 2 });

            // Assert
            Assert.Equal(1, Grid.GetRow(button));
            Assert.Equal(2, Grid.GetColumn(button));
        }

        [Fact]
        public void AddChild_WithInvalidRow_ShouldThrowException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2);
            var button = new Button { Content = "Test" };

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, new LayoutParams { Row = 5, Column = 0 }));

            Assert.Contains("Row 5 is invalid", ex.Message);
            Assert.Contains("Grid has 2 rows", ex.Message);
        }

        [Fact]
        public void AddChild_WithInvalidColumn_ShouldThrowException()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2);
            var button = new Button { Content = "Test" };

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(button, new LayoutParams { Row = 0, Column = 10 }));

            Assert.Contains("Column 10 is invalid", ex.Message);
            Assert.Contains("Grid has 2 columns", ex.Message);
        }

        [Fact]
        public void AddChild_WithRowSpan_ShouldSetSpan()
        {
            // Arrange
            var layout = new GridLayoutEngine(5, 3);
            var panel = new StackPanel();

            // Act
            layout.AddChild(panel, new LayoutParams { Row = 1, Column = 0, RowSpan = 3 });

            // Assert
            Assert.Equal(1, Grid.GetRow(panel));
            Assert.Equal(3, Grid.GetRowSpan(panel));
        }

        [Fact]
        public void AddChild_WithColumnSpan_ShouldSetSpan()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 5);
            var panel = new StackPanel();

            // Act
            layout.AddChild(panel, new LayoutParams { Row = 0, Column = 1, ColumnSpan = 2 });

            // Assert
            Assert.Equal(1, Grid.GetColumn(panel));
            Assert.Equal(2, Grid.GetColumnSpan(panel));
        }

        [Fact]
        public void AddChild_WithExcessiveRowSpan_ShouldThrowException()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 3);
            var panel = new StackPanel();

            // Act & Assert - Row 1 + RowSpan 3 = 4, exceeds 3 rows
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(panel, new LayoutParams { Row = 1, Column = 0, RowSpan = 3 }));

            Assert.Contains("RowSpan", ex.Message);
        }

        [Fact]
        public void AddChild_WithExcessiveColumnSpan_ShouldThrowException()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 3);
            var panel = new StackPanel();

            // Act & Assert - Column 2 + ColumnSpan 2 = 4, exceeds 3 columns
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                layout.AddChild(panel, new LayoutParams { Row = 0, Column = 2, ColumnSpan = 2 }));

            Assert.Contains("ColumnSpan", ex.Message);
        }

        [Fact]
        public void AddChild_WithMargin_ShouldApplyMargin()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2);
            var button = new Button { Content = "Test" };

            // Act
            layout.AddChild(button, new LayoutParams
            {
                Row = 0,
                Column = 0,
                Margin = new Thickness(5, 10, 15, 20)
            });

            // Assert
            Assert.Equal(new Thickness(5, 10, 15, 20), button.Margin);
        }

        [Fact]
        public void AddChild_WithAlignment_ShouldApplyAlignment()
        {
            // Arrange
            var layout = new GridLayoutEngine(2, 2);
            var button = new Button { Content = "Test" };

            // Act
            layout.AddChild(button, new LayoutParams
            {
                Row = 0,
                Column = 0,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            // Assert
            Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Bottom, button.VerticalAlignment);
        }

        [Fact]
        public void AddSplitter_ShouldCreateGridSplitter()
        {
            // Arrange
            var layout = new GridLayoutEngine(3, 1);

            // Act
            layout.AddSplitter(1, isVertical: false);

            // Assert
            var grid = layout.Container as Grid;
            var splitter = grid.Children[0] as GridSplitter;
            Assert.NotNull(splitter);
            Assert.Equal(1, Grid.GetRow(splitter));
            Assert.Equal(HorizontalAlignment.Stretch, splitter.HorizontalAlignment);
            Assert.Equal(5, splitter.Height);
        }
    }
}
