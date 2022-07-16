using Xunit;
using DpsPlugin;
using Lumina;
using Lumina.Excel.GeneratedSheets;
using System.IO;
using Machina.FFXIV.Headers;
using System.Runtime.InteropServices;

namespace DpsPlugin.Test {
    public class PotencyTests
    {
        private string LogPath = @"C:\Users\Brian\Projects\DpsPlugin\test.log";
        private string GameDataPath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
        private string AbilitySweepFolder = @"C:\Users\Brian\Projects\DpsPlugin\Data\AbilitySweeps\";

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
            uint ability = 3617; // hard slash from dk
            PotencyParser.Potency potency = PotencyParser.FindPotency(ActionSheet, ActionTransientSheet, ability, 90);
            File.AppendAllText(LogPath, $"Ability {ability} potency: {potency.NormalPotency}");
            Assert.Equal(170, potency.NormalPotency); 
        }

        [Fact]
        public unsafe void DrkAbilitySweepPotencyTest()
        {
            byte[] data = File.ReadAllBytes(AbilitySweepFolder + "@darkknight.binary");

            int packetLength = DpsPlugin.DpsMessageSize;
            int totalPotency = 0;

            fixed(byte* bytePtr = data) {
                System.IntPtr dataPtr = new System.IntPtr((void*)bytePtr);
                for (int i = 0; i < data.Length; i += packetLength) {
                    Server_MessageHeader header = Marshal.PtrToStructure<Server_MessageHeader>(dataPtr);
                    if (header.MessageType == Opcodes.Ability1) {
                        Server_ActionEffectHeader effectHeader = Marshal.PtrToStructure<Server_ActionEffectHeader>(dataPtr);
                        totalPotency += PotencyParser.FindPotency(ActionSheet, ActionTransientSheet, effectHeader.actionId, 90).NormalPotency;
                    }


                    dataPtr += packetLength;
                }
            }

            Assert.Equal(170, totalPotency); 
        }
    }
}
