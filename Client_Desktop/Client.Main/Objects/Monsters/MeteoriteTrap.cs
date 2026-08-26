using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(103, "Meteorite Trap")]
    public class MeteoriteTrap : MonsterObject
    {
        public MeteoriteTrap()
        {
            RenderShadow = false;
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);

            if (World is not WalkableWorldControl world)
                return;

            ushort targetId = LastAttackTargetId;
            Vector3 targetPosition = WorldPosition.Translation;

            if (targetId != 0 && world.TryGetWalkerById(targetId, out var target))
                targetPosition = target.WorldPosition.Translation;

            var effect = new ScrollOfMeteoriteEffect(targetPosition);
            world.Objects.Add(effect);
            _ = effect.Load();
        }
    }
}
