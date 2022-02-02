using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System;

namespace synth
{
    public partial class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        static int _width = 320;
        static int _height = 160;
        static int ps = 3; // pixel size

        static Texture2D pixel;
        public byte[,] board;

        static Color[] _colors = new [] {
            new Color(10, 25, 50),
            new Color(0, 100, 200),
            new Color(200, 100, 0),
            new Color(255, 255, 255)
        };

        static Agents _agents;
        Map _map;

        public Game1() {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize() {
            _graphics.PreferredBackBufferWidth = _width * ps;
            _graphics.PreferredBackBufferHeight = _height * ps;
            _graphics.ApplyChanges();
            Window.Title = "synth";

            board = new byte[_height, _width];

            base.Initialize();
        }

        protected override void LoadContent() {
            _map = new Map("map");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _agents = new Agents(Content);

            pixel = Content.Load<Texture2D>("pixel");
        }

        // loop

        private void ClearBoard(byte[,] board) {
            for (int y = 0; y < _height; y++) {
                for (int x = 0; x < _width; x++) {
                    board[y, x] = 0;
                }
            }
        }

        private void UpdateControls() {
            if (
                GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape)
                )
                Exit();

            _agents.UpdateControls();
        }
        
        protected override void Update(GameTime gameTime) {
            UpdateControls();

            ClearBoard(board);

            _map.Update(gameTime, board);
            _agents.Update(gameTime, board);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.Clear(_colors[0]);

            _spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp);

            for (int y = 0; y < _height; y++) {
                for (int x = 0; x < _width; x++) {
                    if (board[y, x] != 0) {
                        int px = x * ps;
                        int py = y * ps;
                        _spriteBatch.Draw(pixel, new Rectangle(px, py, ps, ps), _colors[board[y, x]]);
                    }
                }
            }

            _agents.Draw(_spriteBatch);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
