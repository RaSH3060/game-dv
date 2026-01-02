// ################################################
// FILE: NormalMapSystem.cs
// ################################################
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace GameEngine
{
    public class NormalMapSystem
    {
        private GraphicsDevice _graphicsDevice;
        private ContentManager _content;
        private Effect _normalMapEffect;

        public NormalMapSystem(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
        }

        public void Initialize()
        {
            try
            {
                // Try to load the normal map effect if available
                _normalMapEffect = _content.Load<Effect>("NormalMapEffect");
            }
            catch
            {
                // Use null if effect is not available
                _normalMapEffect = null;
            }
        }

        public void ApplyNormalMapEffect(SpriteBatch spriteBatch, Texture2D normalMap, Vector2 position)
        {
            // Apply normal map effect if available
            if (_normalMapEffect != null)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, 
                    null, null, _normalMapEffect);
                
                spriteBatch.Draw(normalMap, position, Color.White);
                
                spriteBatch.End();
                spriteBatch.Begin(); // Return to default state
            }
            else
            {
                // Fallback: just draw without normal mapping
                spriteBatch.Draw(normalMap, position, Color.White);
            }
        }
    }
}