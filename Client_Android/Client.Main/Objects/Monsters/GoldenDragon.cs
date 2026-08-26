using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(44, "Golden Dragon")]
    public class GoldenDragon : RedDragon // Inherits from RedDragon
    {
        public GoldenDragon()
        {
            Scale = 0.9f; // Set according to C++ Setting_Monster
        }
        // Load() and sounds inherited
    }
}