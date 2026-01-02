// ################################################
// FILE: LevelEditor.cs
// ################################################
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using System.Text.Json;
using System.Text.Json.Serialization;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Point = Microsoft.Xna.Framework.Point;

namespace GameEngine
{
    public class LevelEditor
    {
        private CoreEngine _game;
        private SpriteBatch _spriteBatch;
        private Dictionary<string, Texture2D> _textures;
        private Dictionary<string, EntityTemplate> _entityTemplates;
        private FontManager _fontManager;
        private NormalMapSystem _normalMapSystem;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;
        private Entity _selectedEntity;
        private string _selectedEntityType = "player";
        private Vector2 _cameraPosition = Vector2.Zero;
        private bool _isActive = false;
        private SpriteFont _font;
        private List<Entity> _levelEntities;

        public bool IsActive 
        { 
            get { return _isActive; } 
            set { _isActive = value; } 
        }

        public LevelEditor(CoreEngine game, SpriteBatch spriteBatch, Dictionary<string, Texture2D> textures, 
            Dictionary<string, EntityTemplate> entityTemplates, FontManager fontManager, NormalMapSystem normalMapSystem)
        {
            _game = game;
            _spriteBatch = spriteBatch;
            _textures = textures;
            _entityTemplates = entityTemplates;
            _fontManager = fontManager;
            _normalMapSystem = normalMapSystem;
            _levelEntities = new List<Entity>();
            _font = _fontManager.DefaultFont;
        }

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _previousKeyboardState = _currentKeyboardState;
            
            _currentMouseState = Mouse.GetState();
            _currentKeyboardState = Keyboard.GetState();

            if (_currentKeyboardState.IsKeyDown(Keys.Tab) && 
                (_previousKeyboardState == null || !_previousKeyboardState.IsKeyDown(Keys.Tab)))
            {
                _isActive = false;
                return;
            }

            if (_currentKeyboardState.IsKeyDown(Keys.D1)) _selectedEntityType = "player";
            else if (_currentKeyboardState.IsKeyDown(Keys.D2)) _selectedEntityType = "enemy_basic";
            else if (_currentKeyboardState.IsKeyDown(Keys.D3)) _selectedEntityType = "platform";
            else if (_currentKeyboardState.IsKeyDown(Keys.D4)) _selectedEntityType = "coin";
            else if (_currentKeyboardState.IsKeyDown(Keys.D5)) _selectedEntityType = "powerup";
            else if (_currentKeyboardState.IsKeyDown(Keys.D6)) _selectedEntityType = "exit_trigger";

            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                var mousePos = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                var worldPos = Vector2.Transform(mousePos, Matrix.Invert(_game._camera.Transform));
                
                if (_entityTemplates.ContainsKey(_selectedEntityType))
                {
                    var newEntity = new Entity(worldPos, _entityTemplates[_selectedEntityType]);
                    _levelEntities.Add(newEntity);
                }
            }

            if (_currentMouseState.RightButton == ButtonState.Pressed && 
                _previousMouseState.RightButton == ButtonState.Released)
            {
                var mousePos = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                var worldPos = Vector2.Transform(mousePos, Matrix.Invert(_game._camera.Transform));
                
                for (int i = _levelEntities.Count - 1; i >= 0; i--)
                {
                    var entity = _levelEntities[i];
                    var entityRect = new Rectangle((int)entity.Position.X, (int)entity.Position.Y, 
                        entity.Template.Width, entity.Template.Height);
                    var mouseRect = new Rectangle((int)worldPos.X, (int)worldPos.Y, 1, 1);
                    
                    if (entityRect.Intersects(mouseRect))
                    {
                        _levelEntities.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            foreach (var entity in _levelEntities)
            {
                entity.Draw(_spriteBatch, _textures, new Dictionary<string, Animation>(), _normalMapSystem);
            }

            var mousePos = new Vector2(_currentMouseState.X, _currentMouseState.Y);
            var worldPos = Vector2.Transform(mousePos, Matrix.Invert(_game._camera.Transform));
            
            if (_entityTemplates.ContainsKey(_selectedEntityType))
            {
                var previewEntity = new Entity(worldPos, _entityTemplates[_selectedEntityType]);
                previewEntity.Draw(_spriteBatch, _textures, new Dictionary<string, Animation>(), _normalMapSystem);
            }

            if (_font != null)
            {
                var editorText = $"Editor Mode - Selected: {_selectedEntityType}\nTab to exit editor\nLeft click to place, Right click to delete";
                _spriteBatch.DrawString(_font, editorText, new Vector2(10, 10), Color.Yellow);
            }
        }
    }
}