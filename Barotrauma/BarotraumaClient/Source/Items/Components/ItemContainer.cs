﻿using System;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma.Items.Components
{
    partial class ItemContainer : ItemComponent, IDrawableComponent
    {
        private Sprite inventoryTopSprite;
        private Sprite inventoryBackSprite;
        private Sprite inventoryBottomSprite;

        private GUICustomComponent guiCustomComponent;

        public Sprite InventoryTopSprite
        {
            get { return inventoryTopSprite; }
        }
        public Sprite InventoryBackSprite
        {
            get { return inventoryBackSprite; }
        }
        public Sprite InventoryBottomSprite
        {
            get { return inventoryBottomSprite; }
        }

        public Sprite ContainedStateIndicator
        {
            get;
            private set;
        }

#if DEBUG
        [Editable]
#endif
        [Serialize("0.0,0.0", false, description: "The position where the contained items get drawn at (offset from the upper left corner of the sprite in pixels).")]
        public Vector2 ItemPos { get; set; }

#if DEBUG
        [Editable]
#endif
        [Serialize("0.0,0.0", false, description: "The interval at which the contained items are spaced apart from each other (in pixels).")]
        public Vector2 ItemInterval { get; set; }
        [Serialize(100, false, description: "How many items are placed in a row before starting a new row.")]
        public int ItemsPerRow { get; set; }

        /// <summary>
        /// Depth at which the contained sprites are drawn. If not set, the original depth of the item sprites is used.
        /// </summary>
        [Serialize(-1.0f, false, description: "Depth at which the contained sprites are drawn. If not set, the original depth of the item sprites is used.")]
        public float ContainedSpriteDepth { get; set; }


        private float itemRotation;
        [Serialize(0.0f, false, description: "The rotation in which the contained sprites are drawn (in degrees).")]
        public float ItemRotation
        {
            get { return MathHelper.ToDegrees(itemRotation); }
            set { itemRotation = MathHelper.ToRadians(value); }
        }

        [Serialize(null, false, description: "An optional text displayed above the item's inventory.")]
        public string UILabel { get; set; }

        [Serialize(false, false, description: "If enabled, the condition of this item is displayed in the indicator that would normally show the state of the contained items." +
            " May be useful for items such as ammo boxes and magazines that spawn projectiles as needed," +
            " and use the condition to determine how many projectiles can be spawned in total.")]
        public bool ShowConditionInContainedStateIndicator
        {
            get;
            set;
        }

        [Serialize(false, false, description: "Should the inventory of this item be kept open when the item is equipped by a character.")]
        public bool KeepOpenWhenEquipped { get; set; }
        [Serialize(false, false, description: "Can the inventory of this item be moved around on the screen by the player.")]
        public bool MovableFrame { get; set; }

        public Vector2 DrawSize
        {
            //use the extents of the item as the draw size
            get { return Vector2.Zero; }
        }

        partial void InitProjSpecific(XElement element)
        {
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "topsprite":
                        inventoryTopSprite = new Sprite(subElement);
                        break;
                    case "backsprite":
                        inventoryBackSprite = new Sprite(subElement);
                        break;
                    case "bottomsprite":
                        inventoryBottomSprite = new Sprite(subElement);
                        break;
                    case "containedstateindicator":
                        ContainedStateIndicator = new Sprite(subElement);
                        break;
                }
            }
            if (GuiFrame == null)
            {
                //if a GUIFrame is not defined in the xml, 
                //we create a full-screen frame and let the inventory position itself on it
                GuiFrame = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null)
                {
                    CanBeFocused = false
                };
                guiCustomComponent = new GUICustomComponent(new RectTransform(Vector2.One, GuiFrame.RectTransform),
                    onDraw: (SpriteBatch spriteBatch, GUICustomComponent component) => { Inventory.Draw(spriteBatch); },
                    onUpdate: null)
                {
                    CanBeFocused = false
                };
            }
            else
            {
                //if a GUIFrame has been defined, draw the inventory inside it
                guiCustomComponent = new GUICustomComponent(new RectTransform(new Vector2(0.9f), GuiFrame.RectTransform, Anchor.Center),
                    onDraw: (SpriteBatch spriteBatch, GUICustomComponent component) => { Inventory.Draw(spriteBatch); },
                    onUpdate: null)
                {
                    CanBeFocused = false
                };
                Inventory.RectTransform = guiCustomComponent.RectTransform;
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool editing = false)
        {
            if (hideItems || (item.body != null && !item.body.Enabled)) { return; }
            DrawContainedItems(spriteBatch);
        }

        public void DrawContainedItems(SpriteBatch spriteBatch)
        {
            Vector2 transformedItemPos = ItemPos * item.Scale;
            Vector2 transformedItemInterval = ItemInterval * item.Scale;
            float currentRotation = itemRotation;

            if (item.body == null)
            {
                if (item.FlippedX)
                {
                    transformedItemPos.X = -transformedItemPos.X;
                    transformedItemPos.X += item.Rect.Width;
                    transformedItemInterval.X = -transformedItemInterval.X;
                }
                if (item.FlippedY)
                {
                    transformedItemPos.Y = -transformedItemPos.Y;
                    transformedItemPos.Y -= item.Rect.Height;
                    transformedItemInterval.Y = -transformedItemInterval.Y;
                }
                transformedItemPos += new Vector2(item.Rect.X, item.Rect.Y);
                if (item.Submarine != null) { transformedItemPos += item.Submarine.DrawPosition; }
            }
            else
            {
                Matrix transform = Matrix.CreateRotationZ(item.body.Rotation);
                if (item.body.Dir == -1.0f)
                {
                    transformedItemPos.X = -transformedItemPos.X;
                    transformedItemInterval.X = -transformedItemInterval.X;
                }
                transformedItemPos = Vector2.Transform(transformedItemPos, transform);
                transformedItemInterval = Vector2.Transform(transformedItemInterval, transform);

                transformedItemPos += item.DrawPosition;

                currentRotation += item.body.Rotation;
            }

            Vector2 currentItemPos = transformedItemPos;

            SpriteEffects spriteEffects = SpriteEffects.None;
            if ((item.body != null && item.body.Dir == -1) || item.FlippedX) { spriteEffects |= SpriteEffects.FlipHorizontally; }
            if (item.FlippedY) { spriteEffects |= SpriteEffects.FlipVertically; }

            int i = 0;
            foreach (Item containedItem in Inventory.Items)
            {
                if (containedItem == null) continue;

                if (AutoInteractWithContained)
                {
                    containedItem.IsHighlighted = item.IsHighlighted;
                    item.IsHighlighted = false;
                }

                if (containedItem.body != null && 
                    Math.Abs(containedItem.body.FarseerBody.Rotation - currentRotation) > 0.001f)
                {
                    containedItem.body.SetTransformIgnoreContacts(containedItem.body.SimPosition, currentRotation);
                }

                Vector2 origin = containedItem.Sprite.Origin;
                if (item.FlippedX) { origin.X = containedItem.Sprite.SourceRect.Width - origin.X; }
                if (item.FlippedY) { origin.Y = containedItem.Sprite.SourceRect.Height - origin.Y; }

                containedItem.Sprite.Draw(
                    spriteBatch,
                    new Vector2(currentItemPos.X, -currentItemPos.Y),
                    containedItem.GetSpriteColor(),
                    origin,
                    - currentRotation,
                    containedItem.Scale,
                    spriteEffects,
                    depth: ContainedSpriteDepth < 0.0f ? containedItem.Sprite.Depth : ContainedSpriteDepth);

                foreach (ItemContainer ic in containedItem.GetComponents<ItemContainer>())
                {
                    if (ic.hideItems) continue;
                    ic.DrawContainedItems(spriteBatch);
                }

                i++;
                if (Math.Abs(ItemInterval.X) > 0.001f && Math.Abs(ItemInterval.Y) > 0.001f)
                {
                    //interval set on both axes -> use a grid layout
                    currentItemPos.X += transformedItemInterval.X;
                    if (i % ItemsPerRow == 0)
                    {
                        currentItemPos.X = transformedItemPos.X;
                        currentItemPos.Y += transformedItemInterval.Y;
                    }
                }
                else
                {
                    currentItemPos += transformedItemInterval;
                }
            }
        }

        public override void UpdateHUD(Character character, float deltaTime, Camera cam)
        {
            if (Inventory.RectTransform != null)
            {
                guiCustomComponent.RectTransform.Parent = Inventory.RectTransform;
            }

            //if the item is in the character's inventory, no need to update the item's inventory 
            //because the player can see it by hovering the cursor over the item
            guiCustomComponent.Visible = item.ParentInventory?.Owner != character && DrawInventory;
            if (!guiCustomComponent.Visible) return;

            Inventory.Update(deltaTime, cam);
        }

        /*public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            //if the item is in the character's inventory, no need to draw the item's inventory 
            //because the player can see it by hovering the cursor over the item
            if (item.ParentInventory?.Owner == character || !DrawInventory) return;
            
            Inventory.Draw(spriteBatch);            
        }*/
    }
}
