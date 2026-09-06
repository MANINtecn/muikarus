using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(236, "Golden Archer")]
    public class GoldenArcher : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Skill/Skeleton02.bmd");
            await base.Load();
            CurrentAction = 0;
        }

        protected override void HandleClick() { }
    }
}
