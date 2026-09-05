using Client.Data.BMD;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class WaterFallObject : ModelObject
    {
        private const float TEXTURE_SCROLL_SPEED = 0.5f;
        private double _accumulatedTime = 0.0;
        private BMDTexCoord[][] _originalTexCoords;
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object94/Object{idx}.bmd");
            await base.Load();

            if (Model?.Meshes != null && Model.Meshes.Length > 0)
            {
                _originalTexCoords = new BMDTexCoord[Model.Meshes.Length][];

                for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
                {
                    var mesh = Model.Meshes[meshIndex];
                    if (mesh.TexCoords != null && mesh.TexCoords.Length > 0)
                    {
                        _originalTexCoords[meshIndex] = new BMDTexCoord[mesh.TexCoords.Length];
                        Array.Copy(mesh.TexCoords, _originalTexCoords[meshIndex], mesh.TexCoords.Length);
                    }
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            // Rebuilding vertex buffers every frame on mobile causes severe 2 FPS lag.
            // Keeping base update ensures smooth 30+ FPS.
        }

        public override void Draw(GameTime gameTime)
        {
            var prevSamplerState = GraphicsDevice.SamplerStates[0];
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);

            GraphicsDevice.SamplerStates[0] = prevSamplerState;
        }
    }
}
