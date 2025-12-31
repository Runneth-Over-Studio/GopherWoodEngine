using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GopherWoodEngine.Runtime;
using RunnethOverStudio.AppToolkit.Modules.ComponentModel;
using System.Threading.Tasks;

namespace TestBed.ViewModels;

public partial class VulkanTestsViewModel : BaseViewModel
{
    [ObservableProperty]
    string _selectedTest = "Hello Triangle";

    [ObservableProperty]
    string[] _tests = ["Hello Triangle", "Something", "Texture Mapping"];

    [RelayCommand]
    private async Task StartEngineAsync()
    {
        EngineConfig engineConfig = new()
        {
            Title = $"{SelectedTest} | Gopher Wood Engine Test Bed",
            Width = 1280,
            Height = 720
        };

        using TestBedGame game = new(engineConfig);
        game.Start();
    }
}
