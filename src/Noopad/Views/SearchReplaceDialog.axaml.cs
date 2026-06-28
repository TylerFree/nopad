using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Noopad.ViewModels;
using System.ComponentModel;

namespace Noopad.Views;

public partial class SearchReplaceDialog : Window
{
    private SearchReplacePanelViewModel? _viewModel;

    public SearchReplaceDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => FocusSearchBox();
        KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.FocusRequested -= FocusSearchBox;
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as SearchReplacePanelViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.FocusRequested += FocusSearchBox;
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchReplacePanelViewModel.IsVisible) && _viewModel?.IsVisible == false)
            OnCloseRequested();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel?.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnCloseRequested()
    {
        Dispatcher.UIThread.Post(Close);
    }

    private void FocusSearchBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var box = this.FindControl<TextBox>("SearchBox");
            box?.Focus();
            box?.SelectAll();
        });
    }
}
