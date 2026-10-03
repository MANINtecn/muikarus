using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    /// <summary>
    /// Extends WorldControl to support click‐to‐move gameplay.
    /// </summary>
    public abstract class WalkableWorldControl : WorldControl
    {
        // --- Fields ---

        private CursorObject _cursor;
        private float _cursorNextMoveTime;
        private int _previousScrollValue;
        private float _targetCameraDistance;
        private float _minCameraDistance;
        private float _maxCameraDistance;

        // --- Properties ---

        /// <summary>
        /// The player's walker object.
        /// </summary>
        public WalkerObject Walker { get; set; }

        /// <summary>
        /// The X coordinate of the tile currently under the mouse.
        /// </summary>
        public byte MouseTileX { get; set; } = 0;

        /// <summary>
        /// The Y coordinate of the tile currently under the mouse.
        /// </summary>
        public byte MouseTileY { get; set; } = 0;

        /// <summary>
        /// Height offset applied when placing the cursor above terrain.
        /// </summary>
        public float ExtraHeight { get; set; }

        // --- Constructors ---

        /// <summary>
        /// Initializes a walkable world with default walker.
        /// </summary>
        public WalkableWorldControl(short worldIndex)
            : base(worldIndex)
        {
            Interactive = true;
            Camera.Instance.ViewFar = 3500f;
        }

        /// <summary>
        /// Initializes a walkable world with a specified walker.
        /// </summary>
        public WalkableWorldControl(short worldIndex, WalkerObject walker)
            : this(worldIndex)
        {
            Walker = walker;
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Camera.Instance.ViewFar = 3500f;
        }

        // --- Lifecycle Methods ---

        public override async Task Load()
        {
            Objects.Add(_cursor = new CursorObject());
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            // some UI overlay has the mouse, skip click-to-move this frame.
            if (Scene != null && Scene.MouseHoverControl != null && Scene.MouseHoverControl != Scene.World)
            {
                // a UI element has focus or mouse over, and it's not the world itself,
                // then the game world shouldn't process its specific click or scroll.
                // The IsMouseInputConsumedThisFrame flag further reinforces this for other inputs.
                base.Update(time);
                return;
            }

            MonsterObject hoveredMonster = Scene?.MouseHoverObject as MonsterObject;

            // Handle click‐to‐move with a simple cooldown
            if (!Scene.IsMouseInputConsumedThisFrame && // check if UI already handled the click
                (Scene.MouseControl == this || Scene.MouseControl == World || Scene.MouseControl == null || Scene.MouseControl is Client.Main.Controls.UI.Game.MobileControlsOverlay) && // ensure this world or its base is the target or no one captured it
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                _cursorNextMoveTime <= 0f)
            {
                CalculateMouseTilePos();

                Point mousePos = MuGame.Instance.Mouse.Position;

                // 1. Check if an NPC was clicked or tapped
                NPCObject clickedNpc = (Scene?.MouseHoverObject as NPCObject) ?? FindNpcAtTouch(mousePos, MouseTileX, MouseTileY);
                Client.Main.Helpers.OnScreenLogger.Log(
                    $"[TAP] tile=({MouseTileX},{MouseTileY}) npc={(clickedNpc != null ? clickedNpc.GetType().Name : "-")}",
                    Microsoft.Extensions.Logging.LogLevel.Information);
                if (clickedNpc != null)
                {
                    Client.Main.Helpers.OnScreenLogger.Log(
                        $"[NPC] Tocou em {clickedNpc.GetType().Name} ({clickedNpc.DisplayName}) id={clickedNpc.NetworkId}!",
                        Microsoft.Extensions.Logging.LogLevel.Information);
                    clickedNpc.OnClick();
                    if (Scene is Client.Main.Scenes.BaseScene bsNpc)
                        bsNpc.SetMouseInputConsumed();
                    _cursorNextMoveTime = 400f;
                    return;
                }

                // 2. Check if a Monster was clicked or attacked
                if (Walker is PlayerObject player)
                {
                    MonsterObject monster = hoveredMonster ?? FindMonsterAtTouch(mousePos, MouseTileX, MouseTileY);
                    if (monster != null)
                    {
                        float attackRange = player.GetAttackRangeTiles();
                        var selectedSkill = MuGame.Network?.GetCharacterState()?.SelectedSkillId;
                        if (selectedSkill.HasValue && selectedSkill.Value > 0)
                        {
                            Client.Main.Helpers.OnScreenLogger.Log(
                                $"[ATK] skill {selectedSkill.Value} -> {monster.GetType().Name} id={monster.NetworkId}",
                                Microsoft.Extensions.Logging.LogLevel.Information);
                            player.UseSkill(1, selectedSkill.Value, monster);
                            if (Scene is Client.Main.Scenes.BaseScene bsSk)
                                bsSk.SetMouseInputConsumed();
                            _cursorNextMoveTime = 400f;
                            return;
                        }

                        if (Vector2.Distance(player.Location, monster.Location) <= attackRange)
                        {
                            player.Attack(monster);
                            if (Scene is Client.Main.Scenes.BaseScene bs)
                                bs.SetMouseInputConsumed();
                            _cursorNextMoveTime = 250f;
                            return;
                        }
                    }
                }

                _cursorNextMoveTime = 250f;
                var newTile = new Vector2(MouseTileX, MouseTileY);

                if (!IsWalkable(newTile))
                    return;

                float worldX = newTile.X * Constants.TERRAIN_SCALE;
                float worldY = newTile.Y * Constants.TERRAIN_SCALE;
                float height = Terrain.RequestTerrainHeight(worldX, worldY) + ExtraHeight;
                _cursor.Position = new Vector3(worldX, worldY, height) + new Vector3(50f, 40f, 0);
                Walker.MoveTo(newTile);
            }
            else if (_cursorNextMoveTime > 0f)
            {
                _cursorNextMoveTime -= (float)time.ElapsedGameTime.TotalMilliseconds;
            }

            var mouseState = MuGame.Instance.Mouse;
            int currentScroll = mouseState.ScrollWheelValue;
            int scrollDiff = currentScroll - _previousScrollValue;
            if (scrollDiff != 0 && !Scene.IsMouseInputConsumedThisFrame) // check if UI already handled scroll
            {
                float zoomChange = scrollDiff / 100f * 100f;
                _targetCameraDistance = MathHelper.Clamp(
                    _targetCameraDistance - zoomChange,
                    _minCameraDistance,
                    _maxCameraDistance);
            }
            _previousScrollValue = currentScroll;

            base.Update(time);
        }

        // --- Helper Methods ---

        /// <summary>
        /// Calculates which terrain tile is under the mouse cursor by raycasting.
        /// </summary>
        private void CalculateMouseTilePos()
        {
            var mousePos = MuGame.Instance.Mouse.Position.ToVector2();
            var viewport = GraphicsManager.Instance.GraphicsDevice.Viewport;

            var near = viewport.Unproject(new Vector3(mousePos, 0f),
                                          Camera.Instance.Projection,
                                          Camera.Instance.View,
                                          Matrix.Identity);
            var far = viewport.Unproject(new Vector3(mousePos, 1f),
                                          Camera.Instance.Projection,
                                          Camera.Instance.View,
                                          Matrix.Identity);

            var ray = new Ray(near, Vector3.Normalize(far - near));
            const float maxDistance = 10000f;
            float step = Constants.TERRAIN_SCALE / 10f;
            float traveled = 0f;

            var lastPos = ray.Position;
            var lastDiff = lastPos.Z - Terrain.RequestTerrainHeight(lastPos.X, lastPos.Y) + ExtraHeight;
            bool hit = false;
            Vector3 hitPos = Vector3.Zero;

            while (traveled < maxDistance)
            {
                traveled += step;
                var pos = ray.Position + ray.Direction * traveled;
                float terrainZ = Terrain.RequestTerrainHeight(pos.X, pos.Y) + ExtraHeight;
                float diff = pos.Z - terrainZ;

                if (lastDiff > 0f && diff <= 0f)
                {
                    float t = lastDiff / (lastDiff - diff);
                    hitPos = Vector3.Lerp(lastPos, pos, t);
                    hit = true;
                    break;
                }

                lastPos = pos;
                lastDiff = diff;
            }

            if (hit)
            {
                int gx = (int)(hitPos.X / Constants.TERRAIN_SCALE);
                int gy = (int)(hitPos.Y / Constants.TERRAIN_SCALE);

                MouseTileX = (byte)Math.Clamp(gx, 0, Constants.TERRAIN_SIZE - 1);
                MouseTileY = (byte)Math.Clamp(gy, 0, Constants.TERRAIN_SIZE - 1);
            }
            else
            {
                MouseTileX = 0;
                MouseTileY = 0;
            }
        }

        /// <summary>
        /// Finds the most likely touched <see cref="NPCObject"/> using screen-space projection, 3D raycast, and tile proximity.
        /// </summary>
        private NPCObject FindNpcAtTouch(Point mousePos, byte tileX, byte tileY)
        {
            NPCObject bestNpc = null;
            float bestScore = float.MaxValue;

            var mouseVec = mousePos.ToVector2();
            var clickTile = new Vector2(tileX, tileY);
            var gd = GraphicsManager.Instance?.GraphicsDevice;
            var cam = Camera.Instance;
            var ray = MuGame.Instance?.MouseRay ?? default;

            foreach (var obj in Objects)
            {
                if (obj is not NPCObject npc)
                    continue;

                if (!npc.Visible || npc.Status == GameControlStatus.Disposed)
                    continue;

                bool matched = false;
                float score = float.MaxValue;

                // 1. 2D Screen-space projection hit test (direct finger touch target on mobile screen)
                if (gd != null && cam != null)
                {
                    Vector3 worldCenter = npc.Position + new Vector3(0, 0, 75f);
                    Vector3 proj = gd.Viewport.Project(worldCenter, cam.Projection, cam.View, Matrix.Identity);
                    if (proj.Z > 0f && proj.Z < 1f)
                    {
                        float screenDist = Vector2.Distance(new Vector2(proj.X, proj.Y), mouseVec);
                        if (screenDist < 75f)
                        {
                            matched = true;
                            score = screenDist; // Lower pixel distance wins
                        }
                    }
                }

                // 2. 3D Raycast against expanded bounding box
                if (!matched && npc.BoundingBoxWorld.Min != Vector3.Zero)
                {
                    var touchBox = new BoundingBox(
                        npc.BoundingBoxWorld.Min - new Vector3(40f, 40f, 20f),
                        npc.BoundingBoxWorld.Max + new Vector3(40f, 40f, 40f));
                    float? rayHit = ray.Intersects(touchBox);
                    if (rayHit.HasValue)
                    {
                        matched = true;
                        score = 100f + rayHit.Value * 0.01f;
                    }
                }

                // 3. Tile space proximity fallback (generous 4.0 tiles tolerance)
                if (!matched)
                {
                    float tileDist = Vector2.Distance(npc.Location, clickTile);
                    if (tileDist <= 4.0f)
                    {
                        matched = true;
                        score = 200f + tileDist * 10f;
                    }
                }

                if (matched && score < bestScore)
                {
                    bestScore = score;
                    bestNpc = npc;
                }
            }

            return bestNpc;
        }

        /// <summary>
        /// Finds the most likely touched <see cref="MonsterObject"/> using screen-space projection, 3D raycast, and tile proximity.
        /// </summary>
        private MonsterObject FindMonsterAtTouch(Point mousePos, byte tileX, byte tileY)
        {
            MonsterObject bestMonster = null;
            float bestScore = float.MaxValue;

            var mouseVec = mousePos.ToVector2();
            var clickTile = new Vector2(tileX, tileY);
            var gd = GraphicsManager.Instance?.GraphicsDevice;
            var cam = Camera.Instance;
            var ray = MuGame.Instance?.MouseRay ?? default;

            foreach (var obj in Objects)
            {
                if (obj is not MonsterObject m)
                    continue;

                if (!m.Visible || m.Status == GameControlStatus.Disposed)
                    continue;

                bool matched = false;
                float score = float.MaxValue;

                // 1. 2D Screen-space projection hit test
                if (gd != null && cam != null)
                {
                    Vector3 worldCenter = m.Position + new Vector3(0, 0, 50f);
                    Vector3 proj = gd.Viewport.Project(worldCenter, cam.Projection, cam.View, Matrix.Identity);
                    if (proj.Z > 0f && proj.Z < 1f)
                    {
                        float screenDist = Vector2.Distance(new Vector2(proj.X, proj.Y), mouseVec);
                        if (screenDist < 75f)
                        {
                            matched = true;
                            score = screenDist;
                        }
                    }
                }

                // 2. 3D Raycast against expanded bounding box
                if (!matched && m.BoundingBoxWorld.Min != Vector3.Zero)
                {
                    var touchBox = new BoundingBox(
                        m.BoundingBoxWorld.Min - new Vector3(40f, 40f, 20f),
                        m.BoundingBoxWorld.Max + new Vector3(40f, 40f, 40f));
                    float? rayHit = ray.Intersects(touchBox);
                    if (rayHit.HasValue)
                    {
                        matched = true;
                        score = 100f + rayHit.Value * 0.01f;
                    }
                }

                // 3. Tile space proximity fallback
                if (!matched)
                {
                    float tileDist = Vector2.Distance(m.Location, clickTile);
                    if (tileDist <= 3.5f)
                    {
                        matched = true;
                        score = 200f + tileDist * 10f;
                    }
                }

                if (matched && score < bestScore)
                {
                    bestScore = score;
                    bestMonster = m;
                }
            }

            return bestMonster;
        }
    }
}