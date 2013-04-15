﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameEngine.DataStructures;
using GameEngine.Drawing;
using GameEngine.GameObjects;
using GameEngine.Info;
using GameEngine.Interfaces;
using GameEngine.Shaders;
using GameEngine.Tiled;
using GameEngine.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// The TeeEngine - the result of my sweat, blood and tears into this project. The TeeEngine is simply a 2D Tile Engine that
/// provides a number of powerful tools and properties to quickly create and design 2D games that rely on tiles as coordinate
/// systems. The Name Tee Engine came from the idea of a TileEngine, ie a TEngine. I have a personal obsession with Tea, so changing
/// the name of the engine to TeeEngine embeds a bit of my personality into this project.
/// </summary>
namespace GameEngine
{
    public class TeeEngine : GameComponent
    {
        public class DrawingOptionsRecord
        {
            /// <summary>
            /// Show Entity debug information when drawing the world view port to the screen.
            /// </summary>
            public bool ShowEntityDebugInfo { get; set; }

            /// <summary>
            /// Shows the QuadTrees bounding boxes when drawing the world viewport.
            /// </summary>
            public bool ShowQuadTree { get; set; }

            /// <summary>
            /// bool value specifying if the tile grid should be shown during render calls.
            /// </summary>
            public bool ShowTileGrid { get; set; }

            /// <summary>
            /// bool value specifying if the bounding boxes for entities should be shown during render calls.
            /// </summary>
            public bool ShowBoundingBoxes { get; set; }

            /// <summary>
            /// bool value specifying if the bounding boxes for each individual drawable component should be shown.
            /// </summary>
            public bool ShowDrawableComponents { get; set; }
        }

        #region Properties and Variables

        /// <summary>
        /// Graphics Device being used by this Engine to Render.
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; private set; }

        /// <summary>
        /// Width resolution of the TeeEngine buffer in pixels.
        /// </summary>
        public int PixelWidth { get; private set; }

        /// <summary>
        /// Height resolution of the TeeEngine buffer in pixels.
        /// </summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// List of all Entities on screen since the last DrawWorldViewPort call.
        /// </summary>
        public List<Entity> EntitiesOnScreen { get; private set; }

        /// <summary>
        /// Currently loaded TiledMap.
        /// </summary>
        public TiledMap Map { get; private set; }

        /// <summary>
        /// Returns a Collection of all the Entities that have been added to this Engine.
        /// </summary>
        public ICollection<Entity> Entities { get { return _entities.Values; } }

        /// <summary>
        /// List of currently registered GameShaders in use by the TeeEngine.
        /// </summary>
        public List<GameShader> GameShaders { get; private set; }

        /// <summary>
        /// Current QuadTree built during the latest Update call.
        /// </summary>
        public QuadTree QuadTree { get; set; }

        /// <summary>
        /// Last GameTime instance when the TeeEngine was Updated.
        /// </summary>
        public GameTime LastUpdateTime { get; private set; }

        /// <summary>
        /// Debug Information that can be accessed by the player.
        /// </summary>
        public DebugInfo DebugInfo { get; private set; }

        /// <summary>
        /// Class that allows the user to specify the settings for numerous drawing options.
        /// </summary>
        public DrawingOptionsRecord DrawingOptions { get; private set; }

        List<Entity> _entityCreate = new List<Entity>();                // A list of entities which need to be added.
        List<Entity> _entityTrash = new List<Entity>();                 // A list of named entities that are in need of removal.
        
        Dictionary<string, Entity> _entities = new Dictionary<string, Entity>();        // Internal storage structure for entities added to the engine.
        RenderTarget2D _inputBuffer;
        RenderTarget2D _outputBuffer;
        RenderTarget2D _dummyBuffer;

        // TODO: Might be smarter to create a class for handling these.
        Stopwatch _watch1;              // Primary diagnostic watch.
        Stopwatch _watch2;              // Secondary diagnostic watch.
        Stopwatch _watch3;              // Tertiary diagnostic watch.
                                           
        int _entityIdCounter = 0;       // Used for automatic assigning of IDs.

        #endregion

        public TeeEngine(Game game, int pixelWidth, int pixelHeight)
            :base(game)
        {
            _watch1 = new Stopwatch();
            _watch2 = new Stopwatch();
            _watch3 = new Stopwatch();

            GraphicsDevice = game.GraphicsDevice;

            DrawingOptions = new DrawingOptionsRecord();
            DrawingOptions.ShowQuadTree = false;
            DrawingOptions.ShowEntityDebugInfo = false;
            DrawingOptions.ShowTileGrid = false;
            DrawingOptions.ShowBoundingBoxes = false;
            DrawingOptions.ShowDrawableComponents = false;

            EntitiesOnScreen = new List<Entity>();

            DebugInfo = new DebugInfo();
            GameShaders = new List<GameShader>();

            SetResolution(pixelWidth, pixelHeight);
            game.Components.Add(this);
        }

        public void LoadContent()
        {
            ContentManager Content = this.Game.Content;

            foreach (ILoadable loadableShader in GameShaders)
                loadableShader.LoadContent(Content);

            //foreach (Entity entity in _entities.Values)
            //    entity.LoadContent(Content);
        }

        public void UnloadContent()
        {
            foreach (Entity entity in _entities.Values)
                entity.UnloadContent();

            if (_inputBuffer != null)
                _inputBuffer.Dispose();

            if (_outputBuffer != null)
                _outputBuffer.Dispose();

            _inputBuffer = null;
            _outputBuffer = null;

            foreach (ILoadable loadableShader in GameShaders)
                loadableShader.UnloadContent();
        }

        public void LoadMap(TiledMap map)
        {
            this.Map = map;
            this.QuadTree = new QuadTree(map.txWidth, map.txHeight, map.pxTileWidth, map.pxTileHeight);
        }

        public void SetResolution(int pixelWidth, int pixelHeight)
        {
            this.PixelWidth = pixelWidth;
            this.PixelHeight = pixelHeight;

            if (Map != null)
                QuadTree = new QuadTree(Map.txWidth, Map.txHeight, Map.pxTileWidth, Map.pxTileHeight);

            if (_outputBuffer != null)
                _outputBuffer.Dispose();

            if (_inputBuffer != null)
                _inputBuffer.Dispose();

            _inputBuffer = new RenderTarget2D(GraphicsDevice, pixelWidth, pixelHeight, false, SurfaceFormat.Bgr565, DepthFormat.Depth24Stencil8);
            _outputBuffer = new RenderTarget2D(GraphicsDevice, pixelWidth, pixelHeight, false, SurfaceFormat.Bgr565, DepthFormat.Depth24Stencil8);

            // Allow all game shaders to become aware of the change in resolution.
            foreach (GameShader shader in GameShaders) shader.SetResolution(pixelWidth, pixelHeight);
        }

        #region Entity Related Functions

        public void AddEntity(Entity entity)
        {
            AddEntity(null, entity);
        }

        public void AddEntity(string name, Entity entity)
        {
            // Assign an automatic, unique entity name if none is supplied.
            if (name == null) name = string.Format("{0}{1}", entity.GetType(), _entityIdCounter++);

            entity.Name = name;
            entity.LoadContent(Game.Content);
            _entityCreate.Add(entity);
        }

        public ICollection<Entity> GetEntities()
        {
            return _entities.Values;
        }

        public Entity GetEntity(string name)
        {
            return _entities[name];
        }

        public bool RemoveEntity(string name)
        {
            if (_entities.ContainsKey(name))
            {
                // Deferred removal is important because
                // we cannot alter the update loops _entities.Values
                // or else a runtime error will occur if an entity
                // removes itself or someone else in an Update call.
                _entityTrash.Add(_entities[name]);
                return true;
            }

            return false;
        }

        public bool RemoveEntity(Entity entity)
        {
            return RemoveEntity(entity.Name);
        }

        #endregion

        #region Shader Related Functions

        public bool IsRegistered(GameShader shader)
        {
            return GameShaders.Contains(shader);
        }

        public void RegisterGameShader(GameShader shader)
        {
            GameShaders.Add(shader);
            shader.LoadContent(this.Game.Content);
            shader.SetResolution(PixelWidth, PixelHeight);
        }

        public bool UnregisterGameShader(GameShader shader)
        {
            shader.UnloadContent();
            return GameShaders.Remove(shader);
        }

        #endregion

        public override void Update(GameTime gameTime)
        {
            LastUpdateTime = gameTime;
            _watch3.Reset();

            // Clear previous entity update times.
            DebugInfo.Reset();

            foreach (string entityId in _entities.Keys)
            {
                Entity entity = _entities[entityId];
                entity.prevBoundingBox = entity.CurrentBoundingBox;

                // Perform any per-entity update logic.
                _watch2.Restart(); 
                entity.Update(gameTime, this);
                DebugInfo.EntityUpdateTimes[entityId] = _watch2.Elapsed;
                DebugInfo.TotalEntityUpdateTime += _watch2.Elapsed;

                // Recalculate the Entities BoundingBox.
                _watch2.Restart();
                entity.CurrentBoundingBox = entity.GetPxBoundingBox(gameTime);
                DebugInfo.TotalEntityUpdateBoundingBoxTime += _watch2.Elapsed;

                // Reset the IsOnScreen variable before the next drawing operation.
                entity.IsOnScreen = false;

                // If the entity has moved, then update his position in the QuadTree.
                _watch3.Start();
                {
                    if (entity.CurrentBoundingBox != entity.prevBoundingBox)
                        QuadTree.Update(entity);
                }
                _watch3.Stop();
            }

            DebugInfo.QuadTreeUpdateTime = _watch3.Elapsed;

            // REMOVE ANY ENTITIES FOUND IN THE ENTITY TRASH
            _watch2.Restart();
            foreach (Entity entity in _entityTrash)
            {
                _entities.Remove(entity.Name);

                entity.Name = null;
                QuadTree.Remove(entity);
            }
            DebugInfo.TotalEntityRemovalTime = _watch2.Elapsed;

            _watch2.Restart();
            foreach (Entity entity in _entityCreate)
            {
                _entities.Add(entity.Name, entity);

                entity.CurrentBoundingBox = entity.GetPxBoundingBox(gameTime);
                entity.prevBoundingBox = entity.CurrentBoundingBox;

                QuadTree.Add(entity);
            }
            DebugInfo.TotalEntityAdditionTime = _watch2.Elapsed;

            _entityCreate.Clear();
            _entityTrash.Clear();
        }

        #region Drawing Code

        private void DrawQuadTree(ViewPortInfo viewPort, SpriteBatch spriteBatch, QuadTreeNode node, Rectangle destRectangle, SpriteFont spriteFont, float globalDispX, float globalDispY)
        {
            if (node == null) return;

            int actualX = (int) Math.Ceiling(node.pxBounds.X * viewPort.ActualZoom - globalDispX);
            int actualY = (int) Math.Ceiling(node.pxBounds.Y * viewPort.ActualZoom - globalDispY);

            // We need to calculate the 'Actual' width and height otherwise drawing might be innacurate when zoomed.
            int actualWidth  = (int) Math.Ceiling(node.pxBounds.Width * viewPort.ActualZoom);
            int actualHeight = (int) Math.Ceiling(node.pxBounds.Height * viewPort.ActualZoom);

            // Only draw leaf nodes which are within the viewport specified.
            if (node.ChildNode1 == null 
                && new Rectangle(actualX, actualY, actualWidth, actualHeight).Intersects(destRectangle))
            {
                string nodeText = node.NodeID.ToString();

                spriteBatch.DrawRectangle(new Rectangle(actualX, actualY, actualWidth, actualHeight), Color.Lime, 0);
                spriteBatch.DrawString(
                    spriteFont,
                    nodeText,
                    new Vector2(actualX + actualWidth / 2.0f, actualY + actualHeight / 2.0f) - spriteFont.MeasureString(nodeText) / 2,
                    Color.Lime
                );
            }

            DrawQuadTree(viewPort, spriteBatch, node.ChildNode1, destRectangle, spriteFont, globalDispX, globalDispY);
            DrawQuadTree(viewPort, spriteBatch, node.ChildNode2, destRectangle, spriteFont, globalDispX, globalDispY);
            DrawQuadTree(viewPort, spriteBatch, node.ChildNode3, destRectangle, spriteFont, globalDispX, globalDispY);
            DrawQuadTree(viewPort, spriteBatch, node.ChildNode4, destRectangle, spriteFont, globalDispX, globalDispY);
        }
        
        public ViewPortInfo DrawWorldViewPort(SpriteBatch spriteBatch, float pxCenterX, float pxCenterY, float zoom, Rectangle pxDestRectangle, Color color, SamplerState samplerState, SpriteFont spriteFont=null)
        {
            ViewPortInfo viewPortInfo = new ViewPortInfo();
            {
                viewPortInfo.pxTileWidth  = (int) Math.Ceiling(Map.pxTileWidth * zoom);
                viewPortInfo.pxTileHeight = (int) Math.Ceiling(Map.pxTileHeight * zoom);

                // Note about ActualZoom Property:
                // because there is a loss of data between to conversion from Map.pxTileWidth * Zoom -> (int)
                // we need to determine what was the actual level of zoom that was applied to the tiles and use that
                // this ensures that entities that will be drawn will be placed correctly on the screen.
                viewPortInfo.ActualZoom = viewPortInfo.pxTileWidth / Map.pxTileWidth;

                viewPortInfo.pxWidth = pxDestRectangle.Width / viewPortInfo.ActualZoom;
                viewPortInfo.pxHeight = pxDestRectangle.Height / viewPortInfo.ActualZoom;

                viewPortInfo.pxTopLeftX = pxCenterX - viewPortInfo.pxWidth / 2.0f;
                viewPortInfo.pxTopLeftY = pxCenterY - viewPortInfo.pxHeight / 2.0f;

                viewPortInfo.TileCountX = (int) Math.Ceiling((double)viewPortInfo.pxWidth / Map.pxTileWidth) + 1;
                viewPortInfo.TileCountY = (int) Math.Ceiling((double)viewPortInfo.pxHeight / Map.pxTileHeight) + 1;

                // Prevent the View from going outisde of the WORLD coordinates.
                if (viewPortInfo.pxTopLeftX < 0) viewPortInfo.pxTopLeftX = 0;
                if (viewPortInfo.pxTopLeftY < 0) viewPortInfo.pxTopLeftY = 0;

                if (viewPortInfo.pxTopLeftX + viewPortInfo.pxWidth >= Map.pxWidth)
                    viewPortInfo.pxTopLeftX = Map.pxWidth - viewPortInfo.pxWidth;
                if (viewPortInfo.pxTopLeftY + viewPortInfo.pxHeight >= Map.pxHeight)
                    viewPortInfo.pxTopLeftY = Map.pxHeight - viewPortInfo.pxHeight;

                // Calculate any decimal displacement required (For Positions with decimal points).
                viewPortInfo.pxDispX = viewPortInfo.pxTopLeftX - ((int)viewPortInfo.pxTopLeftX / Map.pxTileWidth) * Map.pxTileWidth;
                viewPortInfo.pxDispY = viewPortInfo.pxTopLeftY - ((int)viewPortInfo.pxTopLeftY / Map.pxTileHeight) * Map.pxTileHeight;

                viewPortInfo.pxViewPortBounds = new Rectangle(
                    (int) Math.Ceiling(viewPortInfo.pxTopLeftX),
                    (int) Math.Ceiling(viewPortInfo.pxTopLeftY),
                    (int) Math.Ceiling(viewPortInfo.pxWidth),
                    (int) Math.Ceiling(viewPortInfo.pxHeight)
                );
            }

            // RENDER THE GAME WORLD TO THE VIEWPORT RENDER TARGET
            GraphicsDevice.SetRenderTarget(_inputBuffer);
            GraphicsDevice.Clear(Map.Background);

            // DRAW THE WORLD MAP
            _watch1.Restart();

            // Deferred Rendering should be fine for rendering tile as long as we draw tile layers one at a time
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, samplerState, null, null);
            {
                for (int layerIndex = 0; layerIndex < Map.TileLayers.Count; layerIndex++)
                {
                    // DRAW EACH LAYER
                    TileLayer tileLayer = Map.TileLayers[layerIndex];
                    float depth = 1 - (layerIndex / 10000.0f);

                    for (int i = 0; i < viewPortInfo.TileCountX; i++)
                    {
                        for (int j = 0; j < viewPortInfo.TileCountY; j++)
                        {
                            int tileX = (int)(i + viewPortInfo.pxTopLeftX / Map.pxTileWidth);
                            int tileY = (int)(j + viewPortInfo.pxTopLeftY / Map.pxTileHeight);

                            int tileGid = tileLayer[tileX, tileY];

                            Rectangle pxTileDestRect = new Rectangle(
                                (int) Math.Ceiling(i * viewPortInfo.pxTileWidth - viewPortInfo.pxDispX * viewPortInfo.ActualZoom),
                                (int) Math.Ceiling(j * viewPortInfo.pxTileHeight - viewPortInfo.pxDispY * viewPortInfo.ActualZoom),
                                (int) viewPortInfo.pxTileWidth,
                                (int) viewPortInfo.pxTileHeight
                            );

                            if (tileGid != 0 && tileGid != -1)   // NULL or INVALID Tile Gid is ignored
                            {
                                Tile tile = Map.Tiles[tileGid];

                                spriteBatch.Draw(
                                    tile.SourceTexture,
                                    pxTileDestRect,
                                    tile.SourceRectangle,
                                    Color.White,
                                    0, Vector2.Zero,
                                    SpriteEffects.None,
                                    depth
                                );
                            }

                            // DRAW THE TILE LAYER GRID IF ENABLE
                            if (DrawingOptions.ShowTileGrid && layerIndex == Map.TileLayers.Count - 1)
                                spriteBatch.DrawRectangle(pxTileDestRect, Color.Black, 0);
                        }
                    }
                }
            }
            spriteBatch.End();
            DebugInfo.TileRenderingTime = _watch1.Elapsed;

            // Calculate the entity Displacement caused by pxTopLeft at a global scale to prevent jittering.
            // Each entity should be displaced by the *same amount* based on the pxTopLeftX/Y values
            // this is to prevent entities 'jittering on the screen' when moving the camera.
            float globalDispX = (int) Math.Ceiling(viewPortInfo.pxTopLeftX * viewPortInfo.ActualZoom);
            float globalDispY = (int) Math.Ceiling(viewPortInfo.pxTopLeftY * viewPortInfo.ActualZoom);

            // DRAW VISIBLE REGISTERED ENTITIES
            _watch1.Restart();
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, samplerState, null, null);
            {
                EntitiesOnScreen = QuadTree.GetIntersectingEntites(new FRectangle(viewPortInfo.pxViewPortBounds));

                foreach (Entity entity in EntitiesOnScreen)
                {
                    _watch2.Restart();

                    if (!entity.Visible) continue;
                    entity.IsOnScreen = true;

                    Vector2 pxAbsEntityPos = new Vector2(
                        entity.Pos.X * viewPortInfo.ActualZoom - globalDispX,
                        entity.Pos.Y * viewPortInfo.ActualZoom - globalDispY
                    );

                    FRectangle pxAbsBoundingBox = new FRectangle(
                        entity.CurrentBoundingBox.X * viewPortInfo.ActualZoom - globalDispX,
                        entity.CurrentBoundingBox.Y * viewPortInfo.ActualZoom - globalDispY,
                        entity.CurrentBoundingBox.Width * viewPortInfo.ActualZoom,
                        entity.CurrentBoundingBox.Height * viewPortInfo.ActualZoom
                    );

                    // DRAW ENTITY BOUNDING BOXES IF ENABLED
                    if (DrawingOptions.ShowBoundingBoxes)
                    {
                        spriteBatch.DrawRectangle(pxAbsBoundingBox.ToRectangle(), Color.Red, 0.0001f);
                        SpriteBatchExtensions.DrawCross(
                            spriteBatch,
                            new Vector2(
                                (int) Math.Ceiling(pxAbsEntityPos.X), 
                                (int) Math.Ceiling(pxAbsEntityPos.Y)
                            ), 
                            13, Color.Black, 0f);
                    }

                    foreach (GameDrawableInstance drawable in entity.Drawables.GetByState(entity.CurrentDrawableState))
                    {
                        if (!drawable.Visible) continue;

                        // The relative position of the object should always be (X,Y) - (globalDispX, globalDispY). globalDispX and globalDispY
                        // are based on viewPortInfo.TopLeftX and viewPortInfo.TopLeftY. viewPortInfo.TopLeftX and viewPortInfo.TopLeftY have 
                        // already been corrected in terms of the bounds of the WORLD map coordinates. This allows for panning at the edges.
                        Rectangle pxCurrentFrame = drawable.GetSourceRectangle(LastUpdateTime);

                        int pxObjectWidth  = (int) Math.Ceiling(pxCurrentFrame.Width * entity.ScaleX * viewPortInfo.ActualZoom);
                        int pxObjectHeight = (int) Math.Ceiling(pxCurrentFrame.Height * entity.ScaleY * viewPortInfo.ActualZoom);

                        // Draw the Object based on the current Frame dimensions and the specified Object Width Height values.
                        Rectangle objectDestRect = new Rectangle(
                                (int) Math.Ceiling(pxAbsEntityPos.X) + (int) Math.Ceiling(drawable.Offset.X * viewPortInfo.ActualZoom),
                                (int) Math.Ceiling(pxAbsEntityPos.Y) + (int) Math.Ceiling(drawable.Offset.Y * viewPortInfo.ActualZoom),
                                pxObjectWidth,
                                pxObjectHeight
                        );

                        Vector2 drawableOrigin = new Vector2(
                            (float) Math.Ceiling(drawable.Drawable.Origin.X * pxCurrentFrame.Width),
                            (float) Math.Ceiling(drawable.Drawable.Origin.Y * pxCurrentFrame.Height)
                            );

                        Color drawableColor = new Color()
                        {
                            R = drawable.Color.R,
                            G = drawable.Color.G,
                            B = drawable.Color.B,
                            A = (byte)(drawable.Color.A * entity.Opacity)
                        };

                        // Layer depth should depend how far down the object is on the map (Relative to Y).
                        // Important to also take into account the animation layers for the entity.
                        float layerDepth = Math.Min(0.99f, 1 / (entity.Pos.Y + ((float)drawable.Layer / Map.pxHeight)));

                        spriteBatch.Draw(
                            drawable.GetSourceTexture(LastUpdateTime),
                            objectDestRect,
                            pxCurrentFrame,
                            drawableColor,
                            drawable.Rotation,
                            drawableOrigin,
                            drawable.SpriteEffects,
                            layerDepth);

                        // DRAW INDIVIDUAL DRAWABLE COMPONENTS
                        if (DrawingOptions.ShowDrawableComponents)
                        {
                            Rectangle drawableComponentRect = new Rectangle(
                                (int) Math.Floor(objectDestRect.X - objectDestRect.Width * drawable.Drawable.Origin.X),
                                (int) Math.Floor(objectDestRect.Y - objectDestRect.Height * drawable.Drawable.Origin.Y),
                                objectDestRect.Width, objectDestRect.Height);

                            SpriteBatchExtensions.DrawRectangle(
                                spriteBatch, drawableComponentRect, Color.Blue, 0);
                        }

                        // DRAW ENTITY DETAILS IF ENABLED (ENTITY DEBUG INFO)
                        if (DrawingOptions.ShowEntityDebugInfo)
                        {
                            string message = string.Format(
                                "Pos={0}, Lyr={1}\nBB: {2}\nDR: {3}", 
                                entity.Pos, layerDepth.ToString("0.000"),
                                pxAbsBoundingBox.ToString("0.0"), 
                                objectDestRect);

                            SpriteBatchExtensions.DrawMultiLineString(
                                spriteBatch,
                                spriteFont,
                                message,
                                new Vector2(
                                    objectDestRect.X - objectDestRect.Width * drawable.Drawable.Origin.X + objectDestRect.Width / 2, 
                                    objectDestRect.Y - objectDestRect.Height * drawable.Drawable.Origin.Y + objectDestRect.Height / 2
                                    ),
                                Color.Red);
                        }
                    }

                    DebugInfo.EntityRenderingTimes[entity.Name] = _watch2.Elapsed;
                }
            }
            spriteBatch.End();
            DebugInfo.TotalEntityRenderingTime = _watch1.Elapsed;

            // DRAW THE QUAD TREE IF ENABLED
            if (DrawingOptions.ShowQuadTree)
            {
                spriteBatch.Begin();
                DrawQuadTree(
                    viewPortInfo, 
                    spriteBatch, 
                    QuadTree.Root, 
                    pxDestRectangle, 
                    spriteFont,
                    globalDispX, globalDispY);
                spriteBatch.End();
            }

            _watch1.Restart();
            // APPLY GAME SHADERS TO THE RESULTANT IMAGE
            for (int i = 0; i < GameShaders.Count; i++)
            {
                if (GameShaders[i].Enabled)
                {
                    GameShaders[i].ApplyShader(spriteBatch, viewPortInfo, LastUpdateTime, _inputBuffer, _outputBuffer);

                    // Swap buffers after each render.
                    _dummyBuffer = _inputBuffer;
                    _inputBuffer = _outputBuffer;
                    _outputBuffer = _dummyBuffer;
                }
            }
            DebugInfo.TotalGameShaderRenderTime = _watch1.Elapsed;

            // DRAW THE VIEWPORT TO THE STANDARD SCREEN
            GraphicsDevice.SetRenderTarget(null);
            spriteBatch.Begin();
            {
                spriteBatch.Draw(_inputBuffer, pxDestRectangle, color);
            }
            spriteBatch.End();

            return viewPortInfo;
        }

        #endregion
    }
}
