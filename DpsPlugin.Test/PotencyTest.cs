using Xunit;
using DpsPlugin;
using Lumina;
using Lumina.Excel.GeneratedSheets;
using System.IO;

namespace DpsPlugin.Test {
    public class PotencyTests
    {
        private string LogPath = @"C:\Users\Brian\Projects\DpsPlugin\test.log";
        private string GameDataPath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";

        private Lumina.GameData Lumina { get; init; }
        private Lumina.Excel.ExcelSheet<Action> ActionSheet { get; init; }
        private Lumina.Excel.ExcelSheet<ActionTransient> ActionTransientSheet { get; init; }

        public PotencyTests()
        {
            if (File.Exists(LogPath)) {
                File.Delete(LogPath);
            }
            File.AppendAllText(LogPath, $"Running tests...\n");

            Lumina = new Lumina.GameData(GameDataPath);
            ActionSheet = Lumina.GetExcelSheet<Action>()!;
            ActionTransientSheet = Lumina.GetExcelSheet<ActionTransient>()!;
            PotencyParser.Log = x => File.AppendAllText(LogPath, x);
        }

        [Fact]
        public void Test1()
        {
            int ability = 3617;
            int potency = PotencyParser.FindPotency(ActionSheet, ActionTransientSheet, ability, 90);
            File.AppendAllText(LogPath, $"Ability {ability} potency: {potency}");
            Assert.Equal(170, potency); 
        }
    }
}
