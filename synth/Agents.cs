using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace synth
{
    public partial class Game1 : Game
    {
        public class Agents
        {
            public class Agent
            {
                public string Name;
                public Texture2D Sprite;
                public Vector2 Pos;
                private Vector2 PrevPos;
                public Vector2 Vel;
                public float Speed = 2f;
                private Synth _synth;

                public Agent(string name, string tile, Vector2 pos, OscillatorDelegate oscillator = null) {
                    Name = name;
                    Pos = pos;

                    Vel = new Vector2();

                    if (oscillator != null) {
                        _synth = new Synth(oscillator);
                        _synth.NoteOn((int)Pos.X);
                        _synth.NoteOn((int)Pos.Y);
                    }

                    // generate sprite

                    byte[,] tiledata = _tiles[tile];
                    int sw = tiledata.GetLength(1);
                    int sh = tiledata.GetLength(0);
                    Sprite = new Texture2D(_device, sw, sh);
                    Color[] data = new Color[sw * sh];
                    int i = 0;
                    for (int y=0; y<sh; y++) {
                        for (int x=0; x<sw; x++) {
                            data[i] = _colors[tiledata[y, x]];

                            i++;
                        }
                    }
                    Sprite.SetData(data);
                }

                public void Update(GameTime gameTime, byte[,] board) {
                    if (_synth != null) {
                        _synth.Update(gameTime, board);
                    }
                }

                public void Move(Vector2 direction) {
                    PrevPos = Pos;

                    direction.Normalize();
                    direction = Vector2.Multiply(direction, Speed);
                    Pos = Vector2.Add(Pos, direction);

                    if (_synth != null) {
                        if (Pos.X != PrevPos.X) {
                            _synth.NoteOff((int)PrevPos.X);
                            _synth.NoteOn((int)Pos.X);
                        }
                        if (Pos.Y != PrevPos.Y) {
                            _synth.NoteOff((int)PrevPos.Y);
                            _synth.NoteOn((int)Pos.Y);
                        }
                    }
                }
            }

            static GraphicsDevice _device;
            static Dictionary<string, byte[,]> _tiles;
            static Dictionary<int, Agent> _agents;

            public Agents(ContentManager Content) {
                _device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters());

                // parse agent tiles

                List<string> data = new List<string>();
                using (var file = TitleContainer.OpenStream(@"data/agents.txt")) {
                    using (StreamReader sr = new StreamReader(file)) {
                        string line;
                        while ((line = sr.ReadLine()) != null) {
                            data.Add(line);
                        }
                    }
                }

                _tiles = new Dictionary<string, byte[,]>();

                int tilecounter = 0;
                string name = "";
                byte[,] tile = new byte[0, 0];
                int sw = 0;
                int sh = 0;
                for (int i=0; i<data.Count; i++) {
                    string line = data[i];

                    if (line.Length == 0) {
                        continue;
                    } else if (Char.IsLetter(line, 1)) {
                        if (sw == 0 && sh == 0 && i+1 < data.Count) {
                            sw = data[i + 1].Length;
                            for (int c=i+1; c<data.Count; c++) {
                                bool eol = false;
                                if (i + c >= data.Count) {
                                    eol = true;
                                } else if (data[i+c].Length == 0 || Char.IsLetter(data[i + c], 1)) {
                                    Debug.WriteLine(line);
                                    Debug.WriteLine(data[i + c]);
                                    eol = true;
                                }

                                if (!eol) {
                                    sh++;
                                } else {
                                    break;
                                }
                            }
                        }
                        name = line;
                        tile = new byte[sh, sw];
                    } else {
                        for (int c = 0; c < line.Length; c++) {
                            byte value;
                            if (line[c] == "0"[0]) {
                                value = 0;
                            } else {
                                value = 1;
                            }
                            tile[tilecounter, c] = value;
                        }
                        tilecounter++;

                        if (tilecounter >= sh) {
                            _tiles[name] = tile;
                            tilecounter = 0;
                        }
                    }
                }

                // generate agents

                _agents = new Dictionary<int, Agent>();

                _agents[0] = new Agent("you", "player", new Vector2(_width / 2, _height / 2), Oscillator.Sine);
                player = _agents[0];
            }

            Agent player;

            public void UpdateControls() {
                Vector2 dir = new Vector2();

                if (Keyboard.GetState().IsKeyDown(Keys.Right))
                    dir.X += 1;
                if (Keyboard.GetState().IsKeyDown(Keys.Left))
                    dir.X -= 1;
                if (Keyboard.GetState().IsKeyDown(Keys.Up))
                    dir.Y -= 1;
                if (Keyboard.GetState().IsKeyDown(Keys.Down))
                    dir.Y += 1;

                if (dir.X != 0 || dir.Y != 0) {
                    player.Move(dir);
                }
            }

            public void Update(GameTime gameTime, byte[,] board) {
                player.Update(gameTime, board);
            }

            public void Draw(SpriteBatch _spriteBatch) {
                _spriteBatch.Draw(
                    player.Sprite,
                    new Rectangle(
                        (int)player.Pos.X * ps,
                        (int)player.Pos.Y * ps,
                        ps * player.Sprite.Width,
                        ps * player.Sprite.Height
                        ),
                    Color.White
                    );
            }
        }
    }
}
