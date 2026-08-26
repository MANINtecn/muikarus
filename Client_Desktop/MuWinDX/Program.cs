using System.Windows.Forms;
using Client.Main;

// Removed hardcoded DEBUG data path

Application.SetHighDpiMode(HighDpiMode.SystemAware);
PerformanceRuntimeTuning.Apply();

using var game = new MuGame();
game.Run();
