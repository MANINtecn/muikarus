using Client.Main.Controls;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.Login;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Worlds
{
    public class NewLoginWorld : WorldControl
    {
        private PlayerObject _player;
        private static readonly ILogger _logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { }).CreateLogger<NewLoginWorld>();

        public NewLoginWorld() : base(worldIndex: 95)
        {
            _player = new PlayerObject();
#if ANDROID || IOS
            Camera.Instance.ViewFar = 3200f; // Optimized for mobile login scene: culls off-screen ocean quads
#else
            Camera.Instance.ViewFar = 10000f;
#endif
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[5] = typeof(ShipObject);
            MapTileObjects[12] = typeof(ShipObject);
            MapTileObjects[13] = typeof(ShipObject);

            MapTileObjects[54] = typeof(WaterSplashObject);
            MapTileObjects[1] = typeof(ShipWaterPathObject);

            MapTileObjects[18] = typeof(BlendedObjects);
            MapTileObjects[7] = typeof(BlendedObjects);
            MapTileObjects[10] = typeof(BlendedObjects);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters (optimized for mobile performance)
            Terrain.WaterSpeed = 0.05f;
            Terrain.DistortionAmplitude = 0.05f;
            Terrain.DistortionFrequency = 0.5f;
            Terrain.WaterFlowDirection = Vector2.UnitY;

            // TODO: We need fix CameraAnglePosition load
            Camera.Instance.Target += new Vector3(0, 0, 650);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (!Visible || _player == null) return;

            if (MuGame.Instance.PrevKeyboard.IsKeyDown(Keys.Delete) && MuGame.Instance.Keyboard.IsKeyUp(Keys.Delete))
            {
                if (Objects.Count > 0)
                {
                    var obj = Objects[0];
                    _logger?.LogDebug($"Removing obj: {obj.Type} -> {obj.ObjectName}");
                    Objects.RemoveAt(0);
                }
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Add))
            {
                Camera.Instance.ViewFar += 10;
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Subtract))
            {
                Camera.Instance.ViewFar -= 10;
            }
        }
    }
}
