﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameEngine.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace ShadowKill.GameObjects
{
    public class Bat : Entity
    {
        public Bat()
        {
            this.Origin = new Vector2(1.0f, 0.5f);
            this.Visible = true;
            this.Width = 1.5f;
            this.Height = 1.5f;
        }

        public override void LoadContent(ContentManager Content)
        {
            LoadAnimationXML("Animations/Monsters/bat.anim", Content, 0);
            CurrentAnimation = "Down";
        }
    }
}