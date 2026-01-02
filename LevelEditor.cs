// ################################################
// FILE: CoreEngine2.cs
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
    public class GameEngine : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Dictionary<string, Texture2D> _textures;
        private Dictionary<string, SoundEffect> _sounds;
        private Dictionary<string, Song> _music;
        private Dictionary<string, Animation> _animations;
        private Dictionary<string, Level> _levels;
        private Dictionary<string, EntityTemplate> _entityTemplates;
        private Dictionary<string, SpriteFont> _fonts;
        private NormalMapSystem _normalMapSystem;
        private List<Entity> _entities;
        private List<Effect> _effects;
        private Camera _camera;
        private Player _player;
        private GameState _gameState;
        protected GameSettings _settings;
        private Level _currentLevel;
        private bool _editorEnabled = true;
        private LevelEditor _editor;
        private FontManager _fontManager;
        private SpriteFont _currentFont;
        private int _currentLevelIndex = 0;
        private bool _isPaused = false;
        protected NetworkManager _networkManager;
        private float _networkUpdateTimer = 0f;
        private const float NETWORK_UPDATE_INTERVAL = 0.1f;
        private ParticleSystem _globalParticleSystem;
        private ScreenShake _screenShake;
        private CutsceneSystem _cutsceneSystem;
        private AudioManager _audioManager;
        private InputManager _inputManager;
        private GameStateManager _gameStateManager;
        private CollisionSystem _collisionSystem;
        private Texture2D _pixelTexture;

        public GameEngine()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _settings = LoadSettings();
            _graphics.PreferredBackBufferWidth = _settings.Resolution.X;
            _graphics.PreferredBackBufferHeight = _settings.Resolution.Y;
        }

        protected override void Initialize()
        {
            _textures = new Dictionary<string, Texture2D>();
            _sounds = new Dictionary<string, SoundEffect>();
            _music = new Dictionary<string, Song>();
            _animations = new Dictionary<string, Animation>();
            _levels = new Dictionary<string, Level>();
            _entityTemplates = new Dictionary<string, EntityTemplate>();
            _fonts = new Dictionary<string, SpriteFont>();
            _entities = new List<Entity>();
            _effects = new List<Effect>();
            _camera = new Camera(GraphicsDevice.Viewport);
            _gameState = GameState.Menu;
            _networkManager = new NetworkManager();
            _networkManager.Initialize();
            _networkManager.OnMessageReceived += OnNetworkMessageReceived;
            _networkManager.OnPlayerConnected += OnPlayerConnected;
            _networkManager.OnPlayerDisconnected += OnPlayerDisconnected;
            _globalParticleSystem = new ParticleSystem();
            _screenShake = new ScreenShake();
            _cutsceneSystem = new CutsceneSystem();
            _audioManager = new AudioManager();
            _inputManager = new InputManager();
            _gameStateManager = new GameStateManager();
            _collisionSystem = new CollisionSystem();
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _fontManager = new FontManager(Content, GraphicsDevice);
            _currentFont = _fontManager.DefaultFont;
            Console.WriteLine($"FontManager initialized. DefaultFont is null: {_currentFont == null}");
            _normalMapSystem = new NormalMapSystem(GraphicsDevice, Content);
            _normalMapSystem.Initialize();
            if (_editorEnabled && _editor == null)
            {
                _editor = new LevelEditor(this, _spriteBatch, _textures, _entityTemplates, _fontManager, _normalMapSystem);
                _editor.IsActive = true;
                Console.WriteLine("Editor initialized and activated");
            }
            LoadAssets();
            InitializeStartScreen();
            _audioManager.SetMusicVolume(_settings.MusicVolume);
            _audioManager.SetSfxVolume(_settings.SfxVolume);
        }

        protected override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _inputManager.Update();

            if (_editorEnabled && _editor != null && _editor.IsActive)
            {
                _editor.Update(gameTime);
                return;
            }

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _networkUpdateTimer += deltaTime;
            if (_networkUpdateTimer >= NETWORK_UPDATE_INTERVAL)
            {
                _networkManager.Update(_networkUpdateTimer);
                _networkUpdateTimer = 0f;
                if (_gameState == GameState.Playing && _player != null && _networkManager.IsConnected)
                {
                    _networkManager.SetLocalPlayerState(_player.Position, _player.Health, _player.Ammo, _player.Health > 0);
                }
            }

            switch (_gameState)
            {
                case GameState.Menu:
                    UpdateMenu(gameTime);
                    break;
                case GameState.Playing:
                    UpdateGameplay(gameTime);
                    break;
                case GameState.Paused:
                    UpdatePause(gameTime);
                    break;
                case GameState.LevelComplete:
                    UpdateLevelComplete(gameTime);
                    break;
                case GameState.GameOver:
                    UpdateGameOver(gameTime);
                    break;
                case GameState.Cutscene:
                    UpdateCutscene(gameTime);
                    break;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_editorEnabled && _editor != null && _editor.IsActive)
            {
                GraphicsDevice.Clear(Color.Black);
                _spriteBatch.Begin();
                _editor.Draw(gameTime);
                _spriteBatch.End();
                return;
            }

            GraphicsDevice.Clear(Color.CornflowerBlue);
            var shakeOffset = _screenShake.GetShakeOffset();
            var cameraTransform = _camera.Transform * Matrix.CreateTranslation(shakeOffset.X, shakeOffset.Y, 0);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, cameraTransform);

            switch (_gameState)
            {
                case GameState.Menu:
                    try { DrawMenu(); } catch (Exception ex) { Console.WriteLine($"DrawMenu error: {ex.Message}"); }
                    break;
                case GameState.Playing:
                    try { DrawGameplay(); } catch (Exception ex) { Console.WriteLine($"DrawGameplay error: {ex.Message}"); }
                    break;
                case GameState.Paused:
                    try { DrawPause(); } catch (Exception ex) { Console.WriteLine($"DrawPause error: {ex.Message}"); }
                    break;
                case GameState.LevelComplete:
                    try { DrawLevelComplete(); } catch (Exception ex) { Console.WriteLine($"DrawLevelComplete error: {ex.Message}"); }
                    break;
                case GameState.GameOver:
                    try { DrawGameOver(); } catch (Exception ex) { Console.WriteLine($"DrawGameOver error: {ex.Message}"); }
                    break;
                case GameState.Cutscene:
                    try { DrawCutscene(); } catch (Exception ex) { Console.WriteLine($"DrawCutscene error: {ex.Message}"); }
                    break;
            }

            _spriteBatch.End();
            _spriteBatch.Begin();
            DrawUI();
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void LoadAssets()
        {
            LoadEntityTemplates();
            LoadLevels();
            LoadTextures();
            LoadSounds();
            LoadAnimations();
            LoadFonts();
        }

        private void LoadEntityTemplates()
        {
            var files = Directory.GetFiles(".", "*.cg", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                Console.WriteLine($"Загрузка файла сущности: {file}");
                try
                {
                    var content = File.ReadAllText(file);
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (!doc.RootElement.TryGetProperty("Id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                        {
                            Console.WriteLine($"Файл {file} не содержит строкового свойства 'Id', пропуск.");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Найдено свойство Id в {file}: {idProp.GetRawText()}");
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine($"Неверный JSON в файле {file}, пропуск.");
                        continue;
                    }
                    var template = JsonSerializer.Deserialize<EntityTemplate>(content);
                    if (template == null)
                    {
                        Console.WriteLine($"Пропущен неверный файл шаблона сущности: {file}");
                        continue;
                    }
                    if (string.IsNullOrEmpty(template.Id))
                    {
                        Console.WriteLine($"Шаблон сущности в файле {file} имеет пустой Id (десериализован), пропуск.");
                        continue;
                    }
                    try
                    {
                        if (!_entityTemplates.ContainsKey(template.Id))
                            _entityTemplates.Add(template.Id, template);
                        else
                            _entityTemplates[template.Id] = template;
                        Console.WriteLine($"Зарегистрирован шаблон сущности: {template.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Не удалось добавить шаблон из {file}. template.Id='{template.Id ?? "(null)"}' Исключение: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось загрузить шаблон сущности из {file}: {ex.Message}");
                }
            }
        }

        private void LoadLevels()
        {
            var files = Directory.GetFiles(".", "level*.cg", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (!doc.RootElement.TryGetProperty("Id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                        {
                            Console.WriteLine($"Файл уровня {file} не содержит Id, пропуск.");
                            continue;
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine($"Неверный JSON в файле уровня {file}, пропуск.");
                        continue;
                    }
                    var level = JsonSerializer.Deserialize<Level>(content);
                    if (level == null || string.IsNullOrEmpty(level.Id))
                    {
                        Console.WriteLine($"Пропущен неверный файл уровня: {file}");
                        continue;
                    }
                    if (!_levels.ContainsKey(level.Id))
                        _levels.Add(level.Id, level);
                    else
                        _levels[level.Id] = level;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось загрузить уровень из {file}: {ex.Message}");
                }
            }
        }

        private void LoadTextures()
        {
            foreach (var template in _entityTemplates.Values)
            {
                if (!string.IsNullOrEmpty(template.Sprite) && !_textures.ContainsKey(template.Sprite))
                {
                    try
                    {
                        var texture = Content.Load<Texture2D>(template.Sprite);
                        _textures[template.Sprite] = texture;
                    }
                    catch
                    {
                        _textures[template.Sprite] = CreatePlaceholderTexture(template.Sprite);
                    }
                }
            }
        }

        private void LoadSounds()
        {
            foreach (var template in _entityTemplates.Values)
            {
                if (!string.IsNullOrEmpty(template.Sound) && !_sounds.ContainsKey(template.Sound))
                {
                    try
                    {
                        var sound = Content.Load<SoundEffect>(template.Sound);
                        _sounds[template.Sound] = sound;
                    }
                    catch
                    {
                        // Заглушка, если звук не найден
                    }
                }
            }
        }

        private void LoadAnimations()
        {
            foreach (var template in _entityTemplates.Values)
            {
                if (template.Animation != null)
                {
                    _animations[template.Id + "_anim"] = template.Animation;
                }
            }
        }

        private void LoadFonts()
        {
            // Загрузка шрифтов происходит через FontManager
        }

        private Texture2D CreatePlaceholderTexture(string name)
        {
            var texture = new Texture2D(GraphicsDevice, 32, 32);
            var colorData = new Color[32 * 32];
            for (int i = 0; i < colorData.Length; i++)
            {
                colorData[i] = Color.Magenta;
            }
            texture.SetData(colorData);
            return texture;
        }

        private GameSettings LoadSettings()
        {
            if (File.Exists("settings.cg"))
            {
                var content = File.ReadAllText("settings.cg");
                return JsonSerializer.Deserialize<GameSettings>(content);
            }
            else
            {
                return new GameSettings
                {
                    Resolution = new Point(1024, 768),
                    Fullscreen = false,
                    SoundEnabled = true,
                    MusicVolume = 0.8f,
                    SfxVolume = 0.8f
                };
            }
        }

        private void InitializeStartScreen()
        {
        }

        private void UpdateMenu(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Enter))
            {
                StartNewGame();
            }
            else if (keyboardState.IsKeyDown(Keys.E) && _editorEnabled)
            {
                _editor.IsActive = true;
            }
        }

        private void DrawMenu()
        {
            var menuText = "СУПЕР КОНТРА СТИЛЬ ИГРА\nНажмите ENTER чтобы начать\nНажмите E чтобы открыть редактор";
            if (_fontManager != null && _fontManager.MenuFont != null)
            {
                var textSize = _fontManager.MenuFont.MeasureString(menuText);
                var position = new Vector2(
                    (GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    (GraphicsDevice.Viewport.Height - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_fontManager.MenuFont, menuText, position, Color.White);
            }
        }

        private void StartNewGame()
        {
            _gameState = GameState.Playing;
            _currentLevelIndex = 0;
            LoadLevel(_currentLevelIndex);
        }

        private void LoadLevel(int levelIndex)
        {
            var levelId = $"level{levelIndex + 1}";
            if (_levels.ContainsKey(levelId))
            {
                _currentLevel = _levels[levelId];
                _entities.Clear();
                foreach (var entityConfig in _currentLevel.Entities)
                {
                    var entity = CreateEntityFromTemplate(entityConfig.TemplateId, entityConfig.Position);
                    if (entity != null)
                    {
                        _entities.Add(entity);
                    }
                }
                var playerSpawn = _currentLevel.Entities.FirstOrDefault(e => e.TemplateId == "player_spawn");
                if (playerSpawn != null)
                {
                    _player = new Player(playerSpawn.Position, _entityTemplates["player"]);
                }
                else
                {
                    _player = new Player(new Vector2(100, 100), _entityTemplates["player"]);
                }
                _entities.Add(_player);
                _fontManager.LoadCustomFontForEvent("level_change");
            }
        }

        private Entity CreateEntityFromTemplate(string templateId, Vector2 position)
        {
            if (_entityTemplates.ContainsKey(templateId))
            {
                var template = _entityTemplates[templateId];
                switch (templateId)
                {
                    case "enemy_basic":
                    case "enemy_aggressive":
                    case "enemy_cautious":
                    case "enemy_ranged":
                    case "enemy_patrol":
                    case "enemy_boss1":
                    case "enemy_boss2":
                    case "enemy_boss3":
                    case "enemy_boss4":
                    case "enemy_boss5":
                        return new Enemy(position, template);
                    case "item_health":
                    case "item_ammo":
                    case "item_coin":
                        return new Item(position, template, GetItemType(templateId));
                    default:
                        return new Entity(position, template);
                }
            }
            return null;
        }

        private ItemType GetItemType(string templateId)
        {
            if (templateId.Contains("health")) return ItemType.Health;
            if (templateId.Contains("ammo")) return ItemType.Ammo;
            if (templateId.Contains("coin")) return ItemType.Coin;
            return ItemType.PowerUp;
        }

        private void UpdateGameplay(GameTime gameTime)
        {
            if (_player == null) return;

            _inputManager.Update();
            _player.Update(gameTime);

            _camera.Position = new Vector2(
                MathHelper.Clamp(_player.Position.X - GraphicsDevice.Viewport.Width / 2, 0, _currentLevel.Width - GraphicsDevice.Viewport.Width),
                MathHelper.Clamp(_player.Position.Y - GraphicsDevice.Viewport.Height / 2, 0, _currentLevel.Height - GraphicsDevice.Viewport.Height)
            );

            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (entity != _player)
                {
                    entity.Update(gameTime);
                    if (entity.CollidesWith(_player))
                    {
                        HandleCollision(entity, _player);
                    }
                }
            }

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                _effects[i].Update(gameTime);
                if (_effects[i].IsFinished)
                {
                    _effects.RemoveAt(i);
                }
            }

            CheckLevelCompletion();
        }

        private void DrawGameplay()
        {
            if (!string.IsNullOrEmpty(_currentLevel.Background))
            {
                if (_textures.ContainsKey(_currentLevel.Background))
                {
                    _spriteBatch.Draw(_textures[_currentLevel.Background], Vector2.Zero, Color.White);
                }
            }

            foreach (var entity in _entities)
            {
                entity.Draw(_spriteBatch, _textures, _animations, _normalMapSystem);
            }

            foreach (var effect in _effects)
            {
                effect.Draw(_spriteBatch);
            }
        }

        private void HandleCollision(Entity entity1, Entity entity2)
        {
            if (entity1.TemplateId.Contains("enemy") || entity2.TemplateId.Contains("enemy"))
            {
                var player = entity1.TemplateId == "player" ? entity1 : entity2;
                var enemy = entity1.TemplateId.Contains("enemy") ? entity1 : entity2;
                player.TakeDamage(enemy.Damage);
                _effects.Add(new Effect(player.Position, "hit_effect"));
            }
            else if (entity1.TemplateId.Contains("item") || entity2.TemplateId.Contains("item"))
            {
                var player = entity1.TemplateId == "player" ? entity1 : entity2;
                var item = entity1.TemplateId.Contains("item") ? (Item)entity1 : (Item)entity2;
                item.Collect(player);
                _entities.Remove(item);
            }
        }

        private void CheckLevelCompletion()
        {
            foreach (var trigger in _currentLevel.Triggers)
            {
                var triggerRect = new Rectangle((int)trigger.Position.X, (int)trigger.Position.Y,
                    trigger.Width, trigger.Height);
                var playerRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y,
                    _player.Template.Width, _player.Template.Height);
                if (triggerRect.Intersects(playerRect))
                {
                    HandleTriggerAction(trigger);
                    return;
                }
            }
            var exitTriggers = _entities.Where(e => e.TemplateId == "exit_trigger").ToList();
            foreach (var trigger in exitTriggers)
            {
                if (trigger.CollidesWith(_player))
                {
                    CompleteLevel();
                    return;
                }
            }
        }

        private void HandleTriggerAction(Trigger trigger)
        {
            switch (trigger.Action.ToLower())
            {
                case "next_level":
                case "portal":
                case "door":
                case "warp_zone":
                case "teleporter":
                case "elevator":
                    TransitionToLevel(trigger.Target);
                    break;
                case "cutscene":
                    _gameState = GameState.Cutscene;
                    _fontManager.LoadCustomFontForEvent("cutscene");
                    break;
                case "save_point":
                    // Сохранить прогресс
                    break;
                default:
                    CompleteLevel();
                    break;
            }
        }

        private void TransitionToLevel(string targetLevel)
        {
            for (int i = 0; i < 10; i++)
            {
                if (_levels.ContainsKey($"level{i + 1}") && _levels[$"level{i + 1}"].Id == targetLevel)
                {
                    _currentLevelIndex = i;
                    LoadLevel(_currentLevelIndex);
                    return;
                }
            }
            CompleteLevel();
        }

        private void CompleteLevel()
        {
            _currentLevelIndex++;
            if (_currentLevelIndex >= 10)
            {
                _gameState = GameState.GameOver;
                _fontManager.LoadCustomFontForEvent("game_over");
            }
            else
            {
                _fontManager.LoadCustomFontForEvent("level_complete");
                LoadLevel(_currentLevelIndex);
            }
        }

        private void UpdatePause(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                _gameState = GameState.Playing;
            }
        }

        private void DrawPause()
        {
            var pauseText = "ПАУЗА\nНажмите ESC чтобы продолжить";
            if (_fontManager != null && _fontManager.DefaultFont != null)
            {
                var textSize = _fontManager.DefaultFont.MeasureString(pauseText);
                var position = new Vector2(
                    (GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    (GraphicsDevice.Viewport.Height - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_fontManager.DefaultFont, pauseText, position, Color.White);
            }
        }

        private void UpdateLevelComplete(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Enter))
            {
                if (_currentLevelIndex < 10)
                {
                    LoadLevel(_currentLevelIndex);
                    _gameState = GameState.Playing;
                }
                else
                {
                    _gameState = GameState.GameOver;
                }
            }
        }

        private void DrawLevelComplete()
        {
            var text = "УРОВЕНЬ ПРОЙДЕН!\nНажмите ENTER чтобы продолжить";
            if (_fontManager != null && _fontManager.EventFont != null)
            {
                var textSize = _fontManager.EventFont.MeasureString(text);
                var position = new Vector2(
                    (GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    (GraphicsDevice.Viewport.Height - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_fontManager.EventFont, text, position, Color.Yellow);
            }
        }

        private void UpdateGameOver(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Enter))
            {
                _gameState = GameState.Menu;
            }
        }

        private void DrawGameOver()
        {
            var text = "ИГРА ОКОНЧЕНА\nНажмите ENTER чтобы вернуться в меню";
            if (_fontManager != null && _fontManager.EventFont != null)
            {
                var textSize = _fontManager.EventFont.MeasureString(text);
                var position = new Vector2(
                    (GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    (GraphicsDevice.Viewport.Height - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_fontManager.EventFont, text, position, Color.Red);
            }
        }

        private void UpdateCutscene(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Enter) || keyboardState.IsKeyDown(Keys.Space))
            {
                _gameState = GameState.Playing;
            }
        }

        private void DrawCutscene()
        {
            GraphicsDevice.Clear(Color.Black);
            var cutsceneText = "ТЕКСТ КАТСЦЕНЫ\nЭто пример катсцены\nНажмите ENTER чтобы продолжить";
            if (_fontManager != null && _fontManager.EventFont != null)
            {
                var textSize = _fontManager.EventFont.MeasureString(cutsceneText);
                var position = new Vector2(
                    (GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    (GraphicsDevice.Viewport.Height - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_fontManager.EventFont, cutsceneText, position, Color.White);
            }
        }

        private void DrawUI()
        {
            if (_player != null && _gameState == GameState.Playing)
            {
                if (_fontManager != null && _fontManager.UIFont != null)
                {
                    var uiText = $"ЗДОРОВЬЕ: {_player.Health}\nБОЕПРИПАСЫ: {_player.Ammo}";
                    _spriteBatch.DrawString(_fontManager.UIFont, uiText, new Vector2(10, 10), Color.White);
                }
            }
            if (_networkManager.IsConnected)
            {
                if (_fontManager != null && _fontManager.UIFont != null)
                {
                    var networkText = $"СЕТЬ: {_networkManager.PlayerCount} ИГРОКОВ";
                    _spriteBatch.DrawString(_fontManager.UIFont, networkText, new Vector2(10, 50), Color.Yellow);
                }
            }
        }

        private void OnNetworkMessageReceived(NetworkMessage message)
        {
            switch (message.Type)
            {
                case NetworkMessageType.PlayerConnect:
                    break;
                case NetworkMessageType.PlayerDisconnect:
                    break;
                case NetworkMessageType.PlayerPosition:
                    UpdateRemotePlayerPosition(message);
                    break;
                case NetworkMessageType.PlayerState:
                    UpdateRemotePlayerState(message);
                    break;
                case NetworkMessageType.LevelChange:
                    HandleLevelChangeSync(message);
                    break;
                case NetworkMessageType.EntityUpdate:
                    HandleEntityUpdate(message);
                    break;
            }
        }

        private void OnPlayerConnected(string playerId)
        {
            Console.WriteLine($"Игрок {playerId} подключился к игре");
        }

        private void OnPlayerDisconnected(string playerId)
        {
            Console.WriteLine($"Игрок {playerId} отключился от игры");
        }

        private void UpdateRemotePlayerPosition(NetworkMessage message)
        {
            if (message.Data.ContainsKey("position") && _networkManager.ConnectedPlayers.ContainsKey(message.SenderId))
            {
                var posData = message.Data["position"].ToString();
                var parts = posData.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], out float x) &&
                    float.TryParse(parts[1], out float y))
                {
                    var player = _networkManager.GetPlayerById(message.SenderId);
                    if (player != null)
                    {
                        player.Position = new Vector2(x, y);
                    }
                }
            }
        }

        private void UpdateRemotePlayerState(NetworkMessage message)
        {
            if (_networkManager.ConnectedPlayers.ContainsKey(message.SenderId))
            {
                var player = _networkManager.GetPlayerById(message.SenderId);
                if (player != null)
                {
                    if (message.Data.ContainsKey("health") && int.TryParse(message.Data["health"].ToString(), out int health))
                        player.Health = health;
                    if (message.Data.ContainsKey("ammo") && int.TryParse(message.Data["ammo"].ToString(), out int ammo))
                        player.Ammo = ammo;
                    if (message.Data.ContainsKey("isAlive") && bool.TryParse(message.Data["isAlive"].ToString(), out bool isAlive))
                        player.IsAlive = isAlive;
                }
            }
        }

        private void HandleLevelChangeSync(NetworkMessage message)
        {
            if (message.Data.ContainsKey("levelId"))
            {
                var levelId = message.Data["levelId"].ToString();
                if (_levels.ContainsKey(levelId))
                {
                    _currentLevel = _levels[levelId];
                    _currentLevelIndex = int.Parse(levelId.Replace("level", "")) - 1;
                    _entities.Clear();
                    foreach (var entityConfig in _currentLevel.Entities)
                    {
                        var entity = CreateEntityFromTemplate(entityConfig.TemplateId, entityConfig.Position);
                        if (entity != null)
                        {
                            _entities.Add(entity);
                        }
                    }
                    if (_player != null)
                    {
                        _entities.Add(_player);
                    }
                }
            }
        }

        private void HandleEntityUpdate(NetworkMessage message)
        {
            if (message.Data.ContainsKey("entityId") && message.Data.ContainsKey("entityData"))
            {
                var entityId = message.Data["entityId"].ToString();
                var entityData = (Dictionary<string, object>)message.Data["entityData"];
            }
        }
    }

    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        LevelComplete,
        GameOver,
        Cutscene
    }

    public class GameSettings
    {
        public Microsoft.Xna.Framework.Point Resolution { get; set; }
        public bool Fullscreen { get; set; }
        public bool SoundEnabled { get; set; }
        public float MusicVolume { get; set; }
        public float SfxVolume { get; set; }
        public Dictionary<string, Microsoft.Xna.Framework.Input.Keys> Controls { get; set; } = new Dictionary<string, Microsoft.Xna.Framework.Input.Keys>();
    }

    public class EntityTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sprite { get; set; }
        public int Width { get; set; } = 32;
        public int Height { get; set; } = 32;
        public int Health { get; set; } = 100;
        public int Damage { get; set; } = 10;
        public float Speed { get; set; } = 100f;
        public bool IsSolid { get; set; } = true;
        public bool IsEnemy { get; set; } = false;
        public string Sound { get; set; }
        public Animation Animation { get; set; }
        public AIType AI { get; set; } = AIType.None;
        public List<Attack> Attacks { get; set; } = new List<Attack>();
        public string[] Effects { get; set; } = new string[0];
    }

    public class Animation
    {
        public string[] Frames { get; set; }
        public int[] FrameDurations { get; set; }
        public bool IsLooping { get; set; } = true;
        public Dictionary<int, string> FrameEvents { get; set; } = new Dictionary<int, string>();
    }

    public class Attack
    {
        public string Type { get; set; }
        public int Damage { get; set; }
        public float Range { get; set; }
        public float Cooldown { get; set; }
    }

    public class Level
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Background { get; set; }
        public int Width { get; set; } = 2048;
        public int Height { get; set; } = 1536;
        public List<LevelEntity> Entities { get; set; } = new List<LevelEntity>();
        public List<Trigger> Triggers { get; set; } = new List<Trigger>();
        public List<Effect> Effects { get; set; } = new List<Effect>();
        public ParallaxLayer[] ParallaxLayers { get; set; } = new ParallaxLayer[0];
    }

    public class LevelEntity
    {
        public string TemplateId { get; set; }
        public Vector2 Position { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class Trigger
    {
        public Vector2 Position { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Action { get; set; }
        public string Target { get; set; }
    }

    public class Effect
    {
        public Vector2 Position { get; set; }
        public string Type { get; set; }
        public bool IsFinished { get; set; } = false;
        public float Lifetime { get; set; } = 1.0f;
        public float ElapsedTime { get; set; } = 0f;
        public Effect(Vector2 position, string type)
        {
            Position = position;
            Type = type;
        }
        public virtual void Update(GameTime gameTime)
        {
            ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (ElapsedTime >= Lifetime)
            {
                IsFinished = true;
            }
        }
        public virtual void Draw(SpriteBatch spriteBatch)
        {
        }
    }

    public class Entity
    {
        public Vector2 Position { get; set; }
        public EntityTemplate Template { get; set; }
        public string TemplateId { get; set; }
        public int Health { get; set; }
        public float Speed { get; set; }
        public bool IsSolid { get; set; }
        public bool IsEnemy { get; set; }
        public int Damage { get; set; }
        public AIType AI { get; set; }
        public float AnimationTime { get; set; } = 0f;
        public int CurrentFrame { get; set; } = 0;
        public Microsoft.Xna.Framework.Rectangle Hitbox { get; set; }

        public Entity(Vector2 position, EntityTemplate template)
        {
            Position = position;
            Template = template;
            TemplateId = template.Id;
            Health = template.Health;
            Speed = template.Speed;
            IsSolid = template.IsSolid;
            IsEnemy = template.IsEnemy;
            Damage = template.Damage;
            AI = template.AI;
            UpdateHitbox();
        }

        public virtual void Update(GameTime gameTime)
        {
            AnimationTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (Template.Animation != null && Template.Animation.Frames.Length > 0)
            {
                var totalDuration = Template.Animation.FrameDurations.Sum();
                if (totalDuration > 0)
                {
                    var frameDuration = Template.Animation.FrameDurations[CurrentFrame];
                    if (AnimationTime >= frameDuration / 1000.0f)
                    {
                        AnimationTime = 0;
                        CurrentFrame++;
                        if (CurrentFrame >= Template.Animation.Frames.Length)
                        {
                            if (Template.Animation.IsLooping)
                            {
                                CurrentFrame = 0;
                            }
                            else
                            {
                                CurrentFrame = Template.Animation.Frames.Length - 1;
                            }
                        }
                    }
                }
            }
            if (IsEnemy && AI != AIType.None)
            {
                ApplyAI();
            }
            UpdateHitbox();
        }

        public void Draw(SpriteBatch spriteBatch, Dictionary<string, Texture2D> textures, Dictionary<string, Animation> animations, NormalMapSystem normalMapSystem = null)
        {
            if (textures.ContainsKey(Template.Sprite))
            {
                var texture = textures[Template.Sprite];
                Texture2D normalMap = null;
                if (normalMapSystem != null)
                {
                    normalMap = normalMapSystem.GetNormalMapForTexture(Template.Sprite);
                }
                if (Template.Animation != null && Template.Animation.Frames.Length > 0)
                {
                    var frameTexture = textures.ContainsKey(Template.Animation.Frames[CurrentFrame]) ?
                        textures[Template.Animation.Frames[CurrentFrame]] : texture;
                    Texture2D frameNormalMap = null;
                    if (normalMapSystem != null && Template.Animation.Frames[CurrentFrame] != null)
                    {
                        frameNormalMap = normalMapSystem.GetNormalMapForTexture(Template.Animation.Frames[CurrentFrame]);
                    }
                    spriteBatch.Draw(frameTexture, Position, Color.White);
                }
                else
                {
                    spriteBatch.Draw(texture, Position, Color.White);
                }
            }
        }

        public bool CollidesWith(Entity other)
        {
            return Hitbox.Intersects(other.Hitbox);
        }

        public void TakeDamage(int damage)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Health = 0;
            }
        }

        private void UpdateHitbox()
        {
            Hitbox = new Microsoft.Xna.Framework.Rectangle(
                (int)Position.X,
                (int)Position.Y,
                Template.Width,
                Template.Height
            );
        }

        private void ApplyAI()
        {
            switch (AI)
            {
                case AIType.Aggressive:
                    break;
                case AIType.Cautious:
                    break;
                case AIType.Ranged:
                    break;
            }
        }
    }

    public class Player : Entity
    {
        public int Ammo { get; set; } = 100;
        public int Score { get; set; } = 0;
        public int Lives { get; set; } = 3;
        public Player(Vector2 position, EntityTemplate template) : base(position, template)
        {
        }
        public override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var movement = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.Left) || keyboardState.IsKeyDown(Keys.A))
                movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.D))
                movement.X += 1;
            if (keyboardState.IsKeyDown(Keys.Up) || keyboardState.IsKeyDown(Keys.W))
                movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.Down) || keyboardState.IsKeyDown(Keys.S))
                movement.Y += 1;
            if (movement.Length() > 0)
            {
                movement.Normalize();
                Position += movement * Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            base.Update(gameTime);
        }
    }

    public enum AIType
    {
        None,
        Aggressive,
        Cautious,
        Ranged,
        Patrol,
        Stationary,
        Boss1,
        Boss2,
        Boss3,
        Boss4,
        Boss5
    }

    public class Camera
    {
        public Matrix Transform { get; private set; }
        public Vector2 Position { get; set; }
        public Camera(Viewport viewport)
        {
            Position = Vector2.Zero;
            Transform = Matrix.Identity;
        }
        public void Update()
        {
            Transform = Matrix.CreateTranslation(new Vector3(-Position.X, -Position.Y, 0));
        }
    }

    public class ParallaxLayer
    {
        public string Texture { get; set; }
        public float Speed { get; set; } = 1.0f;
        public int Depth { get; set; } = 0;
    }
}