using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace synth
{
    public partial class Game1 : Game
    {
        public class Tile
        {
            private byte[,] graphic;

            // animations?
        }

        public class Map
        {
            private readonly Tile[] _tiles;
            private byte[,] bg;

            public int _ts { get; private set; } //tile size
            public int Width { get; private set; }
            public int Height { get; private set; }

            public Map(string filename) {
                // parse map data file
                filename = "data/" + filename + ".txt";
                using (var file = TitleContainer.OpenStream(@filename)) {
                    using (StreamReader sr = new StreamReader(file)) {
                        string line;
                        while ((line = sr.ReadLine()) != null) {
                            //Debug.WriteLine(line);
                        }
                    }
                }

                _ts = 8;
                Width = 20;
                Height = 10;
            }

            public void Update(GameTime gameTime, byte[,] board) {
                board = bg;
            }
        }
    }
}
