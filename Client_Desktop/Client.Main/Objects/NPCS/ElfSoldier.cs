using Client.Data;
using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using System.Threading.Tasks;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;
using System;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(257, "Elf Soldier")]
    public class ElfSoldier : NPCObject
    {
        private new readonly ILogger<ElfSoldier> _logger;

        public ElfSoldier()
        {
            _logger = AppLoggerFactory?.CreateLogger<ElfSoldier>();
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            if (Model == null)
            {
                _logger.LogError("CRITICAL: Could not load base player model 'Player/Player.bmd'. NPC cannot be animated.");
                Status = GameControlStatus.Error;
                return;
            }

            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 25);

            // Set item enhancement level +11 for all equipment parts
            Helm.ItemLevel = 11;
            Armor.ItemLevel = 11;
            Pants.ItemLevel = 11;
            Gloves.ItemLevel = 11;
            Boots.ItemLevel = 11;

            await ConfigureNpcWingsAsync(
                "Item/Wing04.bmd",
                blendMesh: 0,
                blendState: Microsoft.Xna.Framework.Graphics.BlendState.Additive);

            await base.Load();
            
            AnimationSpeed = 25f;
            CurrentAction = (int)PlayerAction.PlayerStopFly;
            Scale = 1.0f;

            var currentBBox = BoundingBoxLocal;
            BoundingBoxLocal = new BoundingBox(currentBBox.Min,
                new Vector3(currentBBox.Max.X, currentBBox.Max.Y, currentBBox.Max.Z + 70f));
        }

        protected override void HandleClick()
        {
            _logger?.LogInformation("Elf Soldier clicked - sending buff request sequence (NetworkId: {NetworkId})", NetworkId);

            // Send complete buff sequence: TalkToNpc -> BuffRequest
            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService != null)
            {
                _ = characterService.SendElfSoldierBuffSequenceAsync(NetworkId);
            }
            else
            {
                _logger?.LogWarning("CharacterService is null - cannot send Elf Soldier buff sequence");
            }
        }
    }
}
