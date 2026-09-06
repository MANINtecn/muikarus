using Client.Main.Content;
using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Networking;
using MUnique.OpenMU.Network.Packets;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(407, "Werewolf Quarrel")]
    public class WerewolfQuarrel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Quarrel.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendTalkToNpcRequestAsync(NetworkId);
            }
        }
    }
}
