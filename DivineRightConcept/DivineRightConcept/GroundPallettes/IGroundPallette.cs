﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace DivineRightConcept.GroundPallettes
{
    //Ground Pallette interface that specifies the required properties and methods that would allow
    //for the Game to proporly be able to render the pallette.
    public interface IGroundPallette : ILoadable
    {
        void DrawGroundTexture(SpriteBatch SpriteBatch, int TileType, Rectangle DesRectangle, FRectangle SrcRectangle);

        Color GetTileColor(int TileType);

        int TileCount { get; }
    }
}