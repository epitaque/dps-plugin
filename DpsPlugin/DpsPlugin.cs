using Dalamud.Data;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using Machina.FFXIV.Headers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DpsPlugin
{
    public class DpsPlugin : IDalamudPlugin
    {
        public string logPath = @"C:\Users\Brian\Projects\DpsPlugin\network.log";
        public string binaryLogPath = @"C:\Users\Brian\Projects\DpsPlugin\network.log.binary";
        public string Name => "Dps Plugin";

        private const string commandName = "/dps";

        private ChatGui ChatGui { get; init; }
        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }
        private GameNetwork GameNetwork;
        private DataManager DataManager { get; init; }
        private Lumina.Excel.ExcelSheet<Action> ActionSheet { get; init; }
        private Lumina.Excel.ExcelSheet<ActionTransient> ActionTransientSheet { get; init; }
        private ObjectTable ObjectTable { get; init; } = null!;
        private PartyList PartyList { get; init; }
        Dictionary<uint, List<AbilityCast>> AbilitiesCast;

        struct AbilityCast {
            uint actionId;
            uint actor;
            uint target;
        };

        public DpsPlugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] GameNetwork gameNetwork,
            [RequiredVersion("1.0")] DataManager dataManager,
            [RequiredVersion("1.0")] ObjectTable objectTable,
            [RequiredVersion("1.0")] PartyList partyList,
            ChatGui chatGui)
        {
            // initialize services
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.ChatGui = chatGui;
            this.GameNetwork = gameNetwork;
            this.DataManager = dataManager;
            this.ObjectTable = objectTable;
            this.PartyList = partyList;

            this.ActionSheet = DataManager.Excel.GetSheet<Action>()!;
            this.ActionTransientSheet = DataManager.Excel.GetSheet<ActionTransient>()!;

            System.Console.WriteLine("Plugin loaded.");
            File.Delete(binaryLogPath);
            File.Delete(logPath);
            File.AppendAllText(logPath, string.Format("DpsPlugin loaded.\n"));

            ChatGui.Print(string.Format("DpsPlugin loaded."));

            gameNetwork.NetworkMessage += this.OnNetworkMessage;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            // you might normally want to embed resources and load them from the manifest stream
            var imagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");
            var goatImage = this.PluginInterface.UiBuilder.LoadImage(imagePath);
            this.PluginUi = new PluginUI(this.Configuration, goatImage);

            this.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            this.PluginUi.Visible = true;
        }

        public void Dispose()
        {
            this.PluginUi.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            this.GameNetwork.NetworkMessage -= this.OnNetworkMessage;
        }

        private void OnCommand(string command, string args)
        {
            ChatGui.Print("ChatGUI works!");
            // in response to the slash command, just display our main ui
            this.PluginUi.Visible = true;
        }

        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            this.PluginUi.SettingsVisible = true;
        }

        private void OnNetworkMessage(System.IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (direction == NetworkMessageDirection.ZoneDown)
            {
                if (opCode == 238) {
                    File.AppendAllText(logPath, $"sourceActorId {sourceActorId}, targetActorId {targetActorId}\n");
                }
                LogPacketInfo(dataPtr, targetActorId);
                // int size = 256;
                // byte[] managedArray = new byte[size];
                
                // Marshal.Copy(dataPtr, managedArray, 0, size);

                // string[] decimalBytes = managedArray.Select(x => x.ToString()).ToArray();
                // string asciiBytes = System.Text.Encoding.ASCII.GetString(managedArray);

                // string result = "";
                // for (int i = 0; i < size; i += 4)
                // {
                //     byte[] tempBytes = new byte[] { managedArray[i], managedArray[i + 1], managedArray[i + 2], managedArray[i + 3] };
                //     // tempBytes.Reverse();
                //     result += string.Format("{0:X2} {1:X2} {2:X2} {3:X2}  {4}  {5} {6} {7} {8}\n", managedArray[i], managedArray[i + 1], managedArray[i + 2], managedArray[i + 3],
                //         System.BitConverter.ToInt32(tempBytes, 0),
                //         asciiBytes[i], asciiBytes[i + 1], asciiBytes[i + 2], asciiBytes[i + 3]);
                // }

                // if (opCode == 238) {
                //     // if (managedArray[50] == 0x1B) {
                //     //     managedArray[50] = 0;
                //     // }
                //     uint estimatedDamage = System.BitConverter.ToUInt32(managedArray, 48);
                //     this.PluginUi.TotalDamage += estimatedDamage;
                //     ChatGui.Print(string.Format("We estimated you just did {0} damage.", estimatedDamage));
                // }

                // File.AppendAllText(logPath, string.Format("opCode {0} sourceActorId {1} targetActorId {2}\n", opCode, sourceActorId, targetActorId));
                // File.AppendAllText(logPath, result);

                // AppendAllBytes(binaryLogPath, managedArray);
            }
        }

        public static int DpsMessageSize {
            get {
                unsafe {
                    return 0x20 + sizeof(Server_MessageHeader) + sizeof(Server_ActionEffectHeader);
                }
            }
        }


        private unsafe void LogPacketInfo(System.IntPtr dataPtr, uint targetActorId) {
            dataPtr -= 0x20;
            Server_MessageHeader header = Marshal.PtrToStructure<Server_MessageHeader>(dataPtr);
            // File.AppendAllText(logPath, $"actor {targetActorId} message type {header.MessageType}\n");

            if (header.MessageType == Opcodes.Ability1) {
                Server_ActionEffectHeader effectHeader = Marshal.PtrToStructure<Server_ActionEffectHeader>(dataPtr);

                File.AppendAllText(logPath, $"actor {targetActorId} cast ability {effectHeader.actionId}\n");

                Action action = this.ActionSheet.Where(a => a.RowId == effectHeader.actionId).FirstOrDefault()!;
                ActionTransient actionTransient = this.ActionTransientSheet.Where(a => a.RowId == effectHeader.actionId).FirstOrDefault()!;
                PlayerCharacter playerCharacter = (this.ObjectTable.SearchById(targetActorId) as PlayerCharacter)!;
                File.AppendAllText(logPath, $"action name {action.Name}\n");

                if (playerCharacter == null) {
                    File.AppendAllText(logPath, "playerCharacter null");
                }

                bool inParty = this.PartyList.Any(pm => pm.ObjectId == targetActorId);

                File.AppendAllText(logPath, $"ability1 length {header.MessageLength}, actorId: {header.ActorID}, type {header.MessageType} effectHeader. actionId {effectHeader.actionId}, action name: {action.Name}, playerCharacter name: {playerCharacter!.Name}, playerCharacter level: {playerCharacter.Level}, inParty: {inParty} effectheader object dump: {JsonConvert.SerializeObject(effectHeader, Formatting.Indented)}\n");
                System.Span<byte> span = new System.Span<byte>(((void*)dataPtr), 0x20 + sizeof(Server_MessageHeader) + sizeof(Server_ActionEffectHeader));
                AppendAllBytes(binaryLogPath, span.ToArray());
            }

            if (header.MessageType == Opcodes.ActorGauge) {
                Server_ActorGauge gaugeHeader = Marshal.PtrToStructure<Server_ActorGauge>(dataPtr);
                File.AppendAllText(logPath, $"actor {targetActorId} gauge {JsonConvert.SerializeObject(gaugeHeader, Formatting.Indented)}\n");

            }
            File.AppendAllText(logPath, $"o {header.MessageType} ");
        }

        public static void AppendAllBytes(string path, byte[] bytes)
        {            
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

        }
    }
}
