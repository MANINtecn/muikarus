using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(251, "Hanzo The Blacksmith")]
    public class Hanzo : NPCObject
    {
        public override bool CanRepair => true;

        public Hanzo()
        {
            BlendMesh = 4;
            BlendMeshLight = 0f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Smith01.bmd");
            await base.Load();
            CurrentAction = 0;
        }

        protected override void HandleClick()
        {
            var service = MuGame.Network?.GetCharacterService();
            if (service != null)
                _ = service.SendTalkToNpcRequestAsync(NetworkId);
        }
    }
}
