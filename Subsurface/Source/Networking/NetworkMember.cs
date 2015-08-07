﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace Subsurface.Networking
{
    enum PacketTypes
    {
        Login,
        LoggedIn,
        LogOut,

        PlayerJoined,
        PlayerLeft,
        KickedOut,

        StartGame,
        EndGame,

        CharacterInfo,

        Chatmessage,
        UpdateNetLobby,

        NetworkEvent,

        Traitor
    }

    class NetworkMember
    {
        public const int DefaultPort = 14242;

        public static string MasterServerUrl = Game1.Config.MasterServerUrl;

        protected static Color[] messageColor = { Color.White, Color.Red, Color.LightBlue, Color.LightGreen };
        
        protected string name;

        protected TimeSpan updateInterval;
        protected DateTime updateTimer;

        protected GUIFrame inGameHUD;
        protected GUIListBox chatBox;
        protected GUITextBox chatMsgBox;

        public int Port;

        private bool crewFrameOpen;
        private GUIButton crewButton;
        private GUIFrame crewFrame;

        protected bool gameStarted;

        public string Name
        {
            get { return name; }
            set
            {
                if (string.IsNullOrEmpty(name)) return;
                name = value;
            }
        }

        public GUIFrame InGameHUD
        {
            get { return inGameHUD; }
        }

        public NetworkMember()
        {
            inGameHUD = new GUIFrame(new Rectangle(0,0,0,0), null, null);
            inGameHUD.CanBeFocused = false;

            int width = 350, height = 100;
            chatBox = new GUIListBox(new Rectangle(
                Game1.GraphicsWidth - 20 - width,
                Game1.GraphicsHeight - 40 - 25 - height,
                width, height),
                Color.White * 0.5f, GUI.style, inGameHUD);

            chatMsgBox = new GUITextBox(
                new Rectangle(chatBox.Rect.X, chatBox.Rect.Y + chatBox.Rect.Height + 20, chatBox.Rect.Width, 25),
                Color.White * 0.5f, Color.Black, Alignment.TopLeft, Alignment.Left, GUI.style, inGameHUD);
            chatMsgBox.Font = GUI.SmallFont;
            chatMsgBox.OnEnter = EnterChatMessage;

            crewButton = new GUIButton(new Rectangle(chatBox.Rect.Right-80, chatBox.Rect.Y-30, 80, 20), "Crew", GUI.style, inGameHUD);
            crewButton.OnClicked = ToggleCrewFrame;
        }

        protected void CreateCrewFrame(List<Character> crew)
        {
            int width = 500, height = 400;

            crewFrame = new GUIFrame(new Rectangle(Game1.GraphicsWidth / 2 - width / 2, Game1.GraphicsHeight / 2 - height / 2, width, height), GUI.style);
            crewFrame.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            GUIListBox crewList = new GUIListBox(new Rectangle(0, 0, 200, 300), Color.White * 0.7f, GUI.style, crewFrame);
            crewList.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
            crewList.OnSelected = SelectCharacter;

            foreach (Character character in crew)
            {
                GUIFrame frame = new GUIFrame(new Rectangle(0, 0, 0, 40), Color.Transparent, null, crewList);
                frame.UserData = character;
                frame.Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
                frame.HoverColor = Color.LightGray * 0.5f;
                frame.SelectedColor = Color.Gold * 0.5f;

                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(40, 0, 0, 25),
                    character.Info.Name + " ("+character.Info.Job.Name+")",
                    Color.Transparent, Color.White,
                    Alignment.Left, Alignment.Left,
                    null, frame);
                textBlock.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                new GUIImage(new Rectangle(-10, -10, 0, 0), character.AnimController.limbs[0].sprite, Alignment.Left, frame);
            }
            
            var closeButton = new GUIButton(new Rectangle(0,0, 80, 20), "Close", Alignment.BottomCenter, GUI.style, crewFrame);
            closeButton.OnClicked = ToggleCrewFrame;
        }

        private bool SelectCharacter(object obj)
        {
            Character character = obj as Character;
            if (obj == null) return false;

            GUIComponent existingFrame = crewFrame.FindChild("selectedcharacter");
            if (existingFrame != null) crewFrame.RemoveChild(existingFrame);
            
            var previewPlayer = new GUIFrame(
                new Rectangle(0,0, 230, 300),
                new Color(0.0f, 0.0f, 0.0f, 0.8f), Alignment.TopRight, GUI.style, crewFrame);
            previewPlayer.Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
            previewPlayer.UserData = "selectedcharacter";

            var infoFrame = character.Info.CreateInfoFrame(previewPlayer);

            return true;
        }

        private bool ToggleCrewFrame(GUIButton button, object obj)
        {
            crewFrameOpen = !crewFrameOpen;
            return true;
        }

        public bool EnterChatMessage(GUITextBox textBox, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            SendChatMessage(Game1.NetworkMember.Name + ": " + message);            

            textBox.Deselect();

            return true;
        }
        
        public void AddChatMessage(string message, ChatMessageType messageType)
        {
            Game1.NetLobbyScreen.NewChatMessage(message, messageColor[(int)messageType]);

            while (chatBox.CountChildren > 20)
            {
                chatBox.RemoveChild(chatBox.children[1]);
            }

            GUITextBlock msg = new GUITextBlock(new Rectangle(0, 0, 0, 20), message,
                ((chatBox.CountChildren % 2) == 0) ? Color.Transparent : Color.Black * 0.1f, messageColor[(int)messageType],
                Alignment.Left, null, null, true);
            msg.Font = GUI.SmallFont;

            msg.Padding = new Vector4(20.0f, 0, 0, 0);

            //float prevScroll = chatBox.BarScroll;

            float prevSize = chatBox.BarSize;
            float oldScroll = chatBox.BarScroll;

            msg.Padding = new Vector4(20, 0, 0, 0);
            chatBox.AddChild(msg);

            if ((prevSize == 1.0f && chatBox.BarScroll == 0.0f) || (prevSize < 1.0f && chatBox.BarScroll == 1.0f)) chatBox.BarScroll = 1.0f;

            GUI.PlayMessageSound();
        }

        public virtual void SendChatMessage(string message, ChatMessageType type = ChatMessageType.Server) { }

        public virtual void Update(float deltaTime) 
        {
            if (gameStarted)
            {
                inGameHUD.Update(deltaTime);

                if (crewFrameOpen) crewFrame.Update(deltaTime);
            }

            if (PlayerInput.KeyHit(Keys.Tab))
            {
                if (chatMsgBox.Selected)
                {
                    chatMsgBox.Text = "";
                    chatMsgBox.Deselect();
                }
                else
                {
                    chatMsgBox.Select();
                }
            }
        }

        public virtual void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (!gameStarted) return;

            inGameHUD.Draw(spriteBatch);

            if (crewFrameOpen) crewFrame.Draw(spriteBatch);
        }

        public virtual void Disconnect() { }

        public static int ByteToPlayerCount(byte byteVal, out int maxPlayers)
        {
            maxPlayers = (byteVal >> 4)+1;

            int playerCount = byteVal & (byte)((1 << 4) - 1);

            return playerCount;
        }

    }

    enum ChatMessageType
    {
        Default, Admin, Dead, Server
    }
}
