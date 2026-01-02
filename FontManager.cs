// ################################################
// FILE: FontManager.cs
// ################################################
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace GameEngine
{
    public class FontManager
    {
        private ContentManager _content;
        private GraphicsDevice _graphicsDevice;
        private Dictionary<string, SpriteFont> _fonts;
        
        public SpriteFont DefaultFont { get; private set; }
        public SpriteFont MenuFont { get; private set; }
        public SpriteFont UIFont { get; private set; }
        public SpriteFont EventFont { get; private set; }

        public FontManager(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _content = content;
            _graphicsDevice = graphicsDevice;
            _fonts = new Dictionary<string, SpriteFont>();
            InitializeFonts();
        }

        private void InitializeFonts()
        {
            try
            {
                // Try to load a default font if available
                DefaultFont = _content.Load<SpriteFont>("defaultFont");
            }
            catch
            {
                // Create a basic font if default font is not available
                DefaultFont = CreateBasicFont();
            }

            MenuFont = DefaultFont;
            UIFont = DefaultFont;
            EventFont = DefaultFont;
        }

        private SpriteFont CreateBasicFont()
        {
            // Create a simple texture that represents a basic character
            var texture = new Texture2D(_graphicsDevice, 1, 1);
            texture.SetData(new[] { Color.White });

            var spriteFont = new SpriteFont(
                texture,
                new List<Rectangle> { new Rectangle(0, 0, 10, 10) },
                new List<Rectangle> { new Rectangle(0, 0, 10, 10) },
                new List<char> { 'A' },
                10,
                0.5f,
                new List<Vector3> { new Vector3(10, 0, 0) },
                new Vector2(0, 0)
            );

            return spriteFont;
        }

        public void LoadCustomFontForEvent(string eventType)
        {
            if (_fonts.ContainsKey(eventType))
            {
                EventFont = _fonts[eventType];
            }
            else
            {
                // Load specific font for event if available
                try
                {
                    var font = _content.Load<SpriteFont>(eventType + "Font");
                    _fonts[eventType] = font;
                    EventFont = font;
                }
                catch
                {
                    // Use default if specific font is not available
                    EventFont = DefaultFont;
                }
            }
        }
    }
}