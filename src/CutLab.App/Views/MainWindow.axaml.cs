using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CutLab.App.ViewModels;

namespace CutLab.App.Views;

public partial class MainWindow : Window
{
    private const double InsertAfterHysteresis = 8;

    private DropTargetInfo? _activeDropTarget;
    private ListBoxItem? _hysteresisRow;
    private bool _hysteresisInsertAfter;
    private int _highlightedCut = -1;
    private int _dragSourceCut = -1;

    public MainWindow()
    {
        InitializeComponent();
        ShotList.AddHandler(InputElement.PointerPressedEvent, OnShotListPointerPressed, RoutingStrategies.Tunnel);
        ShotList.AddHandler(DragDrop.DragOverEvent, OnShotListDragOver);
        ShotList.AddHandler(DragDrop.DropEvent, OnShotListDrop);
        Loaded += OnMainWindowLoaded;
        Closing += OnMainWindowClosing;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SyncShotListColumnWidths();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PersistColumnWidths();
        }
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyShotListHeaderColumnWidths();
        ShotListHeaderGrid.LayoutUpdated += OnShotListHeaderLayoutUpdated;
        SyncShotListColumnWidths();
    }

    private void ApplyShotListHeaderColumnWidths()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var columns = ShotListHeaderGrid.ColumnDefinitions;
        if (columns.Count < 9)
        {
            return;
        }

        columns[0].Width = viewModel.ShotColCutWidth;
        columns[2].Width = viewModel.ShotColSourceWidth;
        columns[4].Width = viewModel.ShotColTargetWidth;
        columns[6].Width = viewModel.ShotColStatusWidth;
    }

    private void OnShotListHeaderLayoutUpdated(object? sender, EventArgs e) =>
        SyncShotListColumnWidths();

    private void SyncShotListColumnWidths()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var columns = ShotListHeaderGrid.ColumnDefinitions;
        if (columns.Count < 9)
        {
            return;
        }

        SetColumnWidth(columns[0].ActualWidth, 48, value => viewModel.ShotColCutWidth = value, viewModel.ShotColCutWidth);
        SetColumnWidth(columns[2].ActualWidth, 64, value => viewModel.ShotColSourceWidth = value, viewModel.ShotColSourceWidth);
        SetColumnWidth(columns[4].ActualWidth, 64, value => viewModel.ShotColTargetWidth = value, viewModel.ShotColTargetWidth);
        SetColumnWidth(columns[6].ActualWidth, 48, value => viewModel.ShotColStatusWidth = value, viewModel.ShotColStatusWidth);
    }

    private static void SetColumnWidth(
        double actualWidth,
        double minWidth,
        Action<GridLength> setter,
        GridLength current)
    {
        if (actualWidth <= 0)
        {
            return;
        }

        var next = CreateColumnWidth(actualWidth, minWidth);
        if (Math.Abs(current.Value - next.Value) > 0.5)
        {
            setter(next);
        }
    }

    private static GridLength CreateColumnWidth(double width, double minWidth) =>
        new(Math.Max(minWidth, width));

    private async void OnShotListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsBusy)
        {
            return;
        }

        if (!e.GetCurrentPoint(ShotList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var row = FindListBoxItem(e.Source as Visual);
        if (row?.DataContext is not PreviewRowViewModel preview || preview.ReorderCut is not int cutNumber)
        {
            return;
        }

        var dragData = new DataTransfer();
        dragData.Add(DataTransferItem.CreateText(cutNumber.ToString()));
        await DragDrop.DoDragDropAsync(e, dragData, DragDropEffects.Move);

        ResetDragState(viewModel);
    }

    private void OnShotListDragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsBusy)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!e.DataTransfer.Formats.Contains(DataFormat.Text)
            || !int.TryParse(e.DataTransfer.TryGetText(), out var movedCut))
        {
            e.DragEffects = DragDropEffects.None;
            ResetDragState(viewModel);
            e.Handled = true;
            return;
        }

        var dropTarget = ResolveDropTarget(e, movedCut);
        if (dropTarget is null)
        {
            e.DragEffects = DragDropEffects.None;
            ResetDragState(viewModel);
            e.Handled = true;
            return;
        }

        if (!viewModel.WouldReorderChange(movedCut, dropTarget.TargetIndexInOriginal))
        {
            e.DragEffects = DragDropEffects.None;
            if (dropTarget != _activeDropTarget)
            {
                _activeDropTarget = dropTarget;
                UpdateDragHint(viewModel, movedCut, dropTarget);
            }

            ClearDropHighlights();
            ReorderInsertIndicator.IsVisible = false;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ApplyDropTargetVisuals(viewModel, movedCut, dropTarget);
        e.Handled = true;
    }

    private async void OnShotListDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsBusy)
        {
            return;
        }

        if (!e.DataTransfer.Formats.Contains(DataFormat.Text))
        {
            return;
        }

        var movedCutText = e.DataTransfer.TryGetText();
        if (!int.TryParse(movedCutText, out var movedCut))
        {
            return;
        }

        e.Handled = true;

        var dropTarget = _activeDropTarget ?? ResolveDropTarget(e, movedCut);
        ResetDragState(viewModel);

        if (dropTarget is null || !viewModel.WouldReorderChange(movedCut, dropTarget.TargetIndexInOriginal))
        {
            return;
        }

        await viewModel.ReorderCutAsync(movedCut, dropTarget.TargetIndexInOriginal);
    }

    private void ApplyDropTargetVisuals(
        MainWindowViewModel viewModel,
        int movedCut,
        DropTargetInfo dropTarget)
    {
        if (dropTarget != _activeDropTarget)
        {
            _activeDropTarget = dropTarget;
            UpdateDragHint(viewModel, movedCut, dropTarget);
            UpdateDropHighlights(movedCut, dropTarget.HighlightCut);

            ReorderInsertIndicator.IsVisible = true;
            ReorderInsertIndicator.Width = Math.Max(0, ShotList.Bounds.Width);
            Canvas.SetLeft(ReorderInsertIndicator, 0);
            Canvas.SetTop(ReorderInsertIndicator, dropTarget.IndicatorY);
        }
    }

    private void UpdateDragHint(MainWindowViewModel viewModel, int movedCut, DropTargetInfo dropTarget)
    {
        viewModel.UpdateReorderDragPreview(
            movedCut,
            dropTarget.TargetIndexInOriginal,
            dropTarget.InsertAfter,
            dropTarget.AnchorCut);
    }

    private void UpdateDropHighlights(int movedCut, int highlightCut)
    {
        if (movedCut == _dragSourceCut && highlightCut == _highlightedCut)
        {
            return;
        }

        ClearDropHighlights();
        _dragSourceCut = movedCut;
        _highlightedCut = highlightCut;

        foreach (var item in GetVisibleListBoxItems())
        {
            if (item.DataContext is not PreviewRowViewModel row || row.ReorderCut is not int cut)
            {
                continue;
            }

            if (cut == movedCut)
            {
                item.Classes.Add("reorder-drag-source");
            }

            if (cut == highlightCut)
            {
                item.Classes.Add("reorder-drop-target");
            }
        }
    }

    private void ClearDropHighlights()
    {
        if (_dragSourceCut < 0 && _highlightedCut < 0)
        {
            return;
        }

        foreach (var item in GetVisibleListBoxItems())
        {
            item.Classes.Remove("reorder-drop-target");
            item.Classes.Remove("reorder-drag-source");
        }

        _dragSourceCut = -1;
        _highlightedCut = -1;
    }

    private void ResetDragState(MainWindowViewModel viewModel)
    {
        _activeDropTarget = null;
        _hysteresisRow = null;
        ClearDropHighlights();
        ReorderInsertIndicator.IsVisible = false;
        viewModel.ClearReorderDragPreview();
    }

    private DropTargetInfo? ResolveDropTarget(DragEventArgs e, int movedCut)
    {
        var positionInShotList = e.GetPosition(ShotList);
        var row = FindListBoxItem(ShotList.InputHitTest(positionInShotList) as Visual)
                  ?? FindRowAtPosition(positionInShotList);

        if (row?.DataContext is PreviewRowViewModel preview && preview.ReorderCut is int targetCut)
        {
            var insertAfter = ResolveInsertAfter(row, e);
            var targetIndexInOriginal = insertAfter
                ? GetTargetIndexForCut(targetCut) + 1
                : GetTargetIndexForCut(targetCut);
            var indicatorPoint = row.TranslatePoint(
                insertAfter ? new Point(0, row.Bounds.Height) : new Point(0, 0),
                ReorderOverlay);

            return new DropTargetInfo(
                targetIndexInOriginal,
                insertAfter,
                targetCut,
                targetCut,
                indicatorPoint?.Y ?? 0);
        }

        var lastItem = GetLastVisibleListBoxItem();
        if (lastItem?.DataContext is PreviewRowViewModel lastPreview
            && lastPreview.ReorderCut is int lastCut)
        {
            var bottom = lastItem.TranslatePoint(new Point(0, lastItem.Bounds.Height), ReorderOverlay)?.Y ?? 0;
            if (positionInShotList.Y >= bottom - InsertAfterHysteresis)
            {
                _hysteresisRow = lastItem;
                _hysteresisInsertAfter = true;
                return new DropTargetInfo(
                    GetTargetIndexForCut(lastCut) + 1,
                    true,
                    lastCut,
                    lastCut,
                    bottom);
            }
        }

        return null;
    }

    private bool ResolveInsertAfter(ListBoxItem row, DragEventArgs e)
    {
        var localY = e.GetPosition(row).Y;
        var midpoint = row.Bounds.Height / 2;

        if (_hysteresisRow == row)
        {
            if (_hysteresisInsertAfter)
            {
                if (localY < midpoint - InsertAfterHysteresis)
                {
                    _hysteresisInsertAfter = false;
                }
            }
            else if (localY > midpoint + InsertAfterHysteresis)
            {
                _hysteresisInsertAfter = true;
            }

            return _hysteresisInsertAfter;
        }

        _hysteresisRow = row;
        _hysteresisInsertAfter = localY >= midpoint;
        return _hysteresisInsertAfter;
    }

    private ListBoxItem? FindRowAtPosition(Point positionInShotList)
    {
        foreach (var item in GetVisibleListBoxItems())
        {
            var top = item.TranslatePoint(new Point(0, 0), ShotList)?.Y ?? double.NaN;
            if (double.IsNaN(top))
            {
                continue;
            }

            var bottom = top + item.Bounds.Height;
            if (positionInShotList.Y >= top && positionInShotList.Y < bottom)
            {
                return item;
            }
        }

        return null;
    }

    private int GetTargetIndexForCut(int targetCut)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return 0;
        }

        return viewModel.GetTargetIndexForCut(targetCut);
    }

    private IEnumerable<ListBoxItem> GetVisibleListBoxItems()
    {
        for (var index = 0; index < ShotList.ItemCount; index++)
        {
            if (ShotList.ContainerFromIndex(index) is ListBoxItem item)
            {
                yield return item;
            }
        }
    }

    private ListBoxItem? GetLastVisibleListBoxItem()
    {
        for (var index = ShotList.ItemCount - 1; index >= 0; index--)
        {
            if (ShotList.ContainerFromIndex(index) is ListBoxItem item)
            {
                return item;
            }
        }

        return null;
    }

    private static ListBoxItem? FindListBoxItem(Visual? visual)
    {
        while (visual is not null and not ListBoxItem)
        {
            visual = visual.GetVisualParent();
        }

        return visual as ListBoxItem;
    }

    private sealed record DropTargetInfo(
        int TargetIndexInOriginal,
        bool InsertAfter,
        int HighlightCut,
        int? AnchorCut,
        double IndicatorY);
}
