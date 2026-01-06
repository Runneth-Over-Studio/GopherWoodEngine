using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GopherWoodEngine.Runtime;
using RunnethOverStudio.AppToolkit.Modules.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace TestBed.ViewModels;

public partial class OpenALTestsViewModel : BaseViewModel
{
    [ObservableProperty]
    string _selectedTest;

    [ObservableProperty]
    string[] _tests = ["Looping Sound", "Music"];

    public OpenALTestsViewModel()
    {
        _selectedTest = Tests.First();
    }

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
