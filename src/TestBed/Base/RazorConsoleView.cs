using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components;
using RunnethOverStudio.AppToolkit.Modules.ComponentModel;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TestBed.Base;

/// <summary>
/// Serves as a base class for Razor views in the MVVM pattern, providing automatic data binding.
/// </summary>
/// <typeparam name="TViewModel">
/// The type of the ViewModel that this view is bound to. Must derive from <see cref="ObservableObject"/>.
/// </typeparam>
/// <remarks>
/// This class automatically subscribes to the ViewModel's <see cref="INotifyPropertyChanged.PropertyChanged"/> 
/// event and triggers a view refresh (<see cref="ComponentBase.StateHasChanged"/>) whenever any property changes.
/// Views inheriting from this class should use the <see cref="ViewModel"/> property to bind to data in their markup.
/// </remarks>
public abstract class RazorConsoleView<TViewModel> : ComponentBase, IDisposable where TViewModel : ObservableObject
{
    /// <summary>
    /// Gets the ViewModel instance associated with this view.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the dependency injection system.
    /// Use this property in Razor markup to bind to ViewModel properties (e.g., <c>@ViewModel.PropertyName</c>).
    /// </remarks>
    [Inject]
    protected TViewModel ViewModel { get; set; } = default!;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (ViewModel is BaseViewModel baseViewModel)
        {
            await baseViewModel.InitializeAsync();
        }
    }

    /// <summary>
    /// Releases the resources used by the view and unsubscribes from ViewModel property change notifications.
    /// </summary>
    public void Dispose()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Refresh view when ViewModel properties change.
        InvokeAsync(StateHasChanged);
    }
}
