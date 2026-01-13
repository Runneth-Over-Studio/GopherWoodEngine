using GopherWoodEngine.Runtime;
using System;
using System.IO;

namespace TestBed;

internal class TestGameAudio(string selectedTest, EngineConfig engineConfig) : GameBase(engineConfig)
{
    private readonly string _selectedTest = selectedTest;
    private readonly string _assetsPath = Path.Combine(AppContext.BaseDirectory, "Base", "Assets");

    public override void Initialize()
    {
        switch (_selectedTest)
        {
            case "Looping Sound":
                Engine.WavePlayer.PlayWave2D(Path.Combine(_assetsPath, "Sounds", "sfx_sounds_interaction23.wav"), looping: true);
                break;
            case "Music":
                Engine.WavePlayer.PlayWave2D(Path.Combine(_assetsPath, "Music", "The_Last_Encounter.wav"));
                break;
            default:
                break;
        }
    }
}
