﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using GameEngine.Drawing.Bitmap;
using GameEngine.Extensions;
using GameEngine.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace GameEngine.Drawing
{
    public enum OriginType { Relative, Absolute } 

    /// <summary>
    /// A DrawableSet is a collection of IGameDrawable instances (each called a DrawableInstance), organised in a first-tier
    /// group of STATES. Whenever a STATE (e.g. Running, Walking) is given to a DrawableSet, it will return a list of all the DrawableInstances
    /// that were stored under that STATE. This provides a very quick and easy way of being able to retrieve items which are related
    /// at one go so that they may be drawn together on the screen. Optionally, a second tier grouping mechanism is given by exposing
    /// a GROUP variable in each DrawableInstance. This second tier grouping mechanism allows for items related to each other, but
    /// not necisseraly stored under the same STATE, to have their values set at one go in a batch operation.
    /// 
    /// DrawableInstances are important becuase they allow draw options to be set per Entity Drawable, without effecting the draw
    /// state of other Entities using the same drawable item. For this reason, properties such as Visibility, Layer, Rotation, Color and 
    /// SpriteEffects to use are all stored in a DrawableInstance rather than an IGameDrawable item.
    /// 
    /// A very brief diagram showing the overview of this structure can be seen below:
    /// 
    /// GameDrawableSet -----> [ STATE0 ] ------> {DrawableInstance1, DrawableInstance2, ..., DrawableInstanceN}
    ///                 -----> [ STATE1 ] ------> ...
    ///                 -----> ...
    ///                 -----> [ STATEN ] ------> ...
    ///                 
    /// Similiarly, the same applies for storing by GROUP. The access times of both these allocations is constant O(1) time due to the
    /// use of seperate dictionaries.
    /// 
    /// DrawableInstance -------> {IGameDrawable, Visible, Rotation, Color, SpriteEffects, Layer }
    /// 
    /// </summary>
    public class DrawableSet
    {
        private Dictionary<string, HashSet<DrawableInstance>> _stateDictionary = new Dictionary<string, HashSet<DrawableInstance>>();
        private Dictionary<string, HashSet<DrawableInstance>> _groupDictionary = new Dictionary<string, HashSet<DrawableInstance>>();

        public bool AddGroup(string group)
        {
            if (!_groupDictionary.ContainsKey(group))
            {
                _groupDictionary.Add(group, new HashSet<DrawableInstance>());
                return true;
            }
            else return false;
        }

        public bool RemoveGroup(string group)
        {
            return _groupDictionary.Remove(group);
        }

        public bool AddState(string state)
        {
            if (!_stateDictionary.ContainsKey(state))
            {
                _stateDictionary.Add(state, new HashSet<DrawableInstance>());
                return true;
            }
            else return false;
        }

        public bool RemoveState(string state)
        {
            return _stateDictionary.Remove(state);
        }

        /// <summary>
        /// Returns a collection of all *states* found within this DrawableSet.
        /// </summary>
        public ICollection<string> GetStates()
        {
            return _stateDictionary.Keys;
        }

        /// <summary>
        /// Returns a collection of all *groups* found within this DrawableSet.
        /// </summary>
        public ICollection<string> GetGroups()
        {
            return _groupDictionary.Keys;
        }

        /// <summary>
        /// Returns all the DrawableInstances associated with the *state* specified in the parameter. If the group
        /// does not exist within this DrawableSet, a null value is returned.
        /// </summary>
        public HashSet<DrawableInstance> GetByState(string state)
        {
            return (state==null)? null: _stateDictionary[state];
        }

        /// <summary>
        /// Returns all the DrawableInstances associated with the *group* specified in the parameter. If the group
        /// does not exist within this DrawableSet, a null value is returned.
        /// </summary>
        public HashSet<DrawableInstance> GetByGroup(string group)
        {
            return (group==null)? null: _groupDictionary[group];
        }

        /// <summary>
        /// Adds the specified IGameDrawable to this DrawableSet under the specified *state*. Optionally, the
        /// IGameDrawable being added can also be associated with a *group*. When an IGameDrawable is added to
        /// a DrawableSet, it is wrapped around a DrawableInstance class that allows properties about the 
        /// drawable to be set such as its Color, Rotation, Visibility etc... This DrawableInstance that is
        /// created to wrap the IGameDrawable is returned by this method.
        /// </summary>
        public DrawableInstance Add(string state, IGameDrawable drawable, string group="")
        {
            if (!_stateDictionary.ContainsKey(state))
                AddState(state);

            if (!_groupDictionary.ContainsKey(group))
                AddGroup(group);

            DrawableInstance instance = new DrawableInstance(drawable);

            instance._associatedGroup = group;
            instance._associatedState = state;

            _stateDictionary[state].Add(instance);
            _groupDictionary[group].Add(instance);

            return instance;
        }

        /// <summary>
        /// Removes the DrawableInstance of the specified drawable from the specified set of this DrawableSet.
        /// Returns a true boolean value specifying if such an instance with the specified was found and removed. If
        /// nothing was removed, a false value is returned.
        /// </summary>
        public bool Remove(IGameDrawable drawable, string state)
        {
            foreach(DrawableInstance instance in _stateDictionary[state])
                if (instance.Drawable == drawable)
                    return _stateDictionary[state].Remove(instance);

            return false;
        }

        /// <summary>
        /// Removes the specified DrawableInstance from this DrawableSet. This will only work if the
        /// DrawableInstance belongs to this DrawableSet - it will not work if it belongs to another.
        /// Returns a true boolean value if the specified instance was found within this set and removed.
        /// If nothing was removed, a false value is returned.
        /// </summary>
        public bool Remove(DrawableInstance drawableInstance)
        {
            bool result = true;
            result &= _stateDictionary[drawableInstance._associatedState].Remove(drawableInstance);
            result &= _groupDictionary[drawableInstance._associatedGroup].Remove(drawableInstance);

            return result;
        }

        /// <summary>
        /// Performs a Set union operation between this DrawableSet and the DrawableSet specified in the parameter.
        /// It is important to note that any IGameDrawables newly created in this DrawableSet are wrapped around a
        /// *new* DrawableInstance in order to allow properties to be set independently for each one.
        /// </summary>
        public void Union(DrawableSet drawableSet)
        {
            foreach (string state in drawableSet.GetStates())
            {
                foreach (DrawableInstance instance in drawableSet.GetByState(state))
                {
                    DrawableInstance copiedInstance = Add(state, instance.Drawable, instance._associatedGroup);
                    copiedInstance.Layer = instance.Layer;
                    copiedInstance.Offset = instance.Offset;
                    copiedInstance.Rotation = instance.Rotation;
                    copiedInstance.Visible = instance.Visible;
                }
            }
        }

        /// <summary>
        /// Performs Set negation between this DrawableSet and the DrawabelSet specified in the parameter. While
        /// two sets should never share the same DrawableInstance, they may share the same IGameDrawable which would
        /// be wrapped by an DrawableInstance. If the DrawableSet specified in the parameter contains IGameDrawables
        /// within the same state as this DrawableSet, then associated DrawableInstance's are removed.
        /// </summary>
        public void Remove(DrawableSet drawableSet)
        {
            foreach (string state in drawableSet.GetStates())
                foreach (DrawableInstance instance in drawableSet.GetByState(state))
                    Remove(instance.Drawable, state);
        }

        /// <summary>
        /// Clears all drawable items from this DrawableSet. After this opereation, this DrawableSet will have
        /// no more States or Groups.
        /// </summary>
        public void ClearAll()
        {
            _stateDictionary.Clear();
            _groupDictionary.Clear();
        }

        public bool IsStateFinished(string state, GameTime gameTime)
        {
            foreach (DrawableInstance instance in _stateDictionary[state])
                if (!instance.IsFinished(gameTime))
                    return false;

            return true;
        }

        public bool IsGroupFinished(string group, GameTime gameTime)
        {
            foreach (DrawableInstance instance in _groupDictionary[group])
                if (!instance.IsFinished(gameTime))
                    return false;

            return true;
        }

        /// <summary>
        /// Asks all IGameDrawables within the specified *state* set to perform a Reset operation.
        /// </summary>
        public void ResetState(string state, GameTime gameTime)
        {
            foreach (DrawableInstance instance in _stateDictionary[state])
                instance.Reset(gameTime);
        }

        /// <summary>
        /// Asks all IGameDrawables within the specified *group* set to perform a Reset operation. By performing a
        /// Reset operation, each GameDrawable should act like it has just been created.
        /// </summary>
        public void ResetGroup(string group, GameTime gameTime)
        {
            foreach (DrawableInstance instance in _groupDictionary[group])
                instance.Reset(gameTime);
        }

        /// <summary>
        /// Sets the specified property for all the DrawableInstances in the specified group using C# Reflection. The
        /// property to be set should be specified as a case sensitive string and its value should be set with
        /// an type that matches the Properties type. If the property specified does not exist in the DrawableInstance
        /// class, then an ArgumentException is thrown.
        /// </summary>
        public void SetGroupProperty(string group, string property, object value)
        {
            PropertyInfo propertyInfo = typeof(DrawableInstance).GetProperty(property);

            if (property == null) throw new ArgumentException(string.Format("The Property '{0}' does not exist", property));

            foreach (DrawableInstance drawable in _groupDictionary[group])
                propertyInfo.SetValue(drawable, value, null);
        }

        /// <summary>
        /// Sets the specified property for all the DrawableInstances in the specified state using C# Reflection. The
        /// property to be set should be specified as a case sensitive string and its value should be set with
        /// an type that matches the Properties type. If the property specified does not exist in the DrawableInstance
        /// class, then an ArgumentException is thrown.
        /// </summary>
        public void SetStateProperty(string state, string property, object value)
        {
            PropertyInfo propertyInfo = typeof(DrawableInstance).GetProperty(property);

            if (property == null) throw new ArgumentException(string.Format("The Property '{0}' does not exist", property));

            foreach (DrawableInstance drawable in _stateDictionary[state])
                propertyInfo.SetValue(drawable, value, null);
        }

        public override string ToString()
        {
            return string.Format("DrawableSet: States={0}, Groups={1}", _stateDictionary.Keys.Count, _groupDictionary.Keys.Count);
        }

        /// <summary>
        /// Loads a DrawableSet file into a specified DrawableSet object.
        /// The method requires the string path to the xml file containing the drawable data and a reference to the
        /// ContentManager. An optional Layer value can be specified for the ordering of the drawables in the 
        /// DrawableSet. Currently only supports loading of Animation objects.
        /// </summary>
        /// <param name="drawableSet">DrawableSet object to load the animations into.</param>
        /// <param name="path">String path to the XML formatted .anim file</param>
        /// <param name="content">Reference to the ContentManager instance being used in the application</param>
        public static void LoadDrawableSetXml(DrawableSet drawableSet, string path, ContentManager content, double startTimeMS = 0)
        {
            XmlDocument document = new XmlDocument();
            document.Load(path);

            // Initialize all declared states.
            foreach (XmlNode stateNode in document.SelectNodes("DrawableSet/States/State"))
                drawableSet.AddState(stateNode.InnerText);

            foreach (XmlNode animNode in document.SelectNodes("DrawableSet/Animations/Animation"))
            {
                int frameDelay = XmlExtensions.GetAttributeValue<int>(animNode, "FrameDelay", 90);
                bool loop = XmlExtensions.GetAttributeValue<bool>(animNode, "Loop", true);
                bool visible = XmlExtensions.GetAttributeValue<bool>(animNode, "Visible", true);
                int layer = XmlExtensions.GetAttributeValue<int>(animNode, "Layer", 0);

                string state = XmlExtensions.GetAttributeValue(animNode, "State");
                string group = XmlExtensions.GetAttributeValue(animNode, "Group", "");
                string spriteSheet = XmlExtensions.GetAttributeValue(animNode, "SpriteSheet");
                OriginType originType = XmlExtensions.GetAttributeValue<OriginType>(animNode, "OriginType", OriginType.Relative);

                Vector2 offset = XmlExtensions.GetAttributeValue<Vector2>(animNode, "Offset", Vector2.Zero);
                Vector2 origin = XmlExtensions.GetAttributeValue<Vector2>(animNode, "Origin", new Vector2(0.5f, 1.0f));

                XmlNodeList frameNodes = animNode.SelectNodes("Frames/Frame");
                Rectangle[] frames = new Rectangle[frameNodes.Count];

                for (int i = 0; i < frameNodes.Count; i++)
                {
                    string[] tokens = frameNodes[i].InnerText.Split(',');
                    if (tokens.Length != 4)
                        throw new FormatException("Expected 4 Values for Frame Definition: X, Y, Width, Height");

                    int x = Convert.ToInt32(tokens[0]);
                    int y = Convert.ToInt32(tokens[1]);
                    int width = Convert.ToInt32(tokens[2]);
                    int height = Convert.ToInt32(tokens[3]);

                    frames[i] = new Rectangle(x, y, width, height);
                }

                Animation animation = new Animation(content.Load<Texture2D>(spriteSheet), frames, frameDelay, loop);
                animation.Origin = origin;

                // TODO: Requires possible revision of code.
                // Allow support for specifying glob patterns in the case of state names.
                if (state.Contains("*"))
                {
                    // Use Glob patterns in favour of regular expressions.
                    state = Regex.Escape(state).Replace(@"\*", ".*").Replace(@"\?", ".");
                    Regex regexMatcher = new Regex(state);

                    foreach (string drawableSetState in drawableSet.GetStates())
                    {
                        if (regexMatcher.IsMatch(drawableSetState))
                        {
                            DrawableInstance instance = drawableSet.Add(drawableSetState, animation, group);
                            instance.StartTimeMS = startTimeMS;
                            instance.Layer = layer;
                            instance.Offset = offset;
                            instance.Visible = visible;
                        }
                    }
                }
                else
                {
                    DrawableInstance instance = drawableSet.Add(state, animation, group);
                    instance.StartTimeMS = startTimeMS;
                    instance.Layer = layer;
                    instance.Offset = offset;
                    instance.Visible = visible;
                }
            }
        }
    }
}
