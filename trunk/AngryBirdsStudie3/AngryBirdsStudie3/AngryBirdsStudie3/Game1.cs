using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Interfaces;
using Gestenerkennung;
using Maussteuerung;

namespace AngryBirdsStudie3
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game, GameInterface
    {
        // Painting devices
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        // Texturen
        Texture2D background;
        Texture2D flitsche;
        Texture2D bird;
        Texture2D gummi;
        Texture2D handOpen;
        Texture2D handClosed;
        Texture2D[] pigs;

        // Drawing Points
        Rectangle mainFrame;
        Point flitschePosition = new Point(50, 730);
        Point initialBirdPosition = new Point(135, 755);
        Point birdPosition = new Point(135, 755);
        Point linkerPfosten = new Point(140, 780);
        Point rechterPfosten = new Point(180, 780);
        enum InterfaceType { InterfaceGesture, InterfaceMouse };
        InterfaceType interfaceType = InterfaceType.InterfaceMouse;
        GestenInterface gameInterface;
        MouseInterface mouseInterface;

        // Pointer parameter
        Point pointerPosition;
        PointerState pointerState;

        // Zooming/Scrolling parameter
        Point cameraPoint = new Point(0, 0);
        float zoomLevel = 1.0f;
        public Matrix TransformMatrix
        {
            get
            {
                return Matrix.CreateRotationZ(0.0f) * Matrix.CreateScale(zoomLevel) *
                       Matrix.CreateTranslation(cameraPoint.X, cameraPoint.Y, 0);
            }
        }

        // Game state parameter
        enum GameState { Idle, Firing, Flying };
        GameState currentState = GameState.Idle;

        // Parabel Parameter y = ax^2  + bx + c
        double a, b, c;
        // Time paramter x = bt - t0 
        double t0, x0;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            this.graphics.PreferredBackBufferWidth = 1920;
            this.graphics.PreferredBackBufferHeight = 1080;
            graphics.IsFullScreen = true;

            if (interfaceType == InterfaceType.InterfaceMouse)
            {
                mouseInterface = new MouseInterface(this);
            }
            else if (interfaceType == InterfaceType.InterfaceGesture)
            {
                gameInterface = new GestenInterface(this);
            }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load the background content.
            background = Content.Load<Texture2D>("background");
            flitsche = Content.Load<Texture2D>("flitsche");
            bird = Content.Load<Texture2D>("bird");
            handOpen = Content.Load<Texture2D>("hand_open");
            handClosed = Content.Load<Texture2D>("hand_closed");
            gummi = new Texture2D(GraphicsDevice, 1, 1);
            gummi.SetData<Color>(
                new Color[] { new Color(135, 61, 39) });
            pigs = new Texture2D[10];
            for (int i = 0; i < pigs.Length; i++)
            {
                pigs[i] = Content.Load<Texture2D>("pig" + (i + 1));
            }

            // Set the rectangle parameters.
            mainFrame = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();

            if (interfaceType == InterfaceType.InterfaceMouse)
            {
                mouseInterface.updateMouseState(Mouse.GetState());
            }

            if (currentState == GameState.Flying)
            {
                double x = 100.0d * (System.Environment.TickCount - t0) / 1000.0d + x0;
                double y = a * Math.Pow(x, 2) + b * x + c;
                //System.Console.WriteLine("x = " + x + " ; y = " + y + " ; t = "+ (System.Environment.TickCount - t0));
                if (y >= 880 && x >= 160)
                {
                    y = 880;
                    currentState = GameState.Idle;
                }

                birdPosition = new Point((int)x, (int)y);
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // Draw the background.

            // Start building the sprite.
            spriteBatch.Begin(0, null, null, null, null, null, TransformMatrix);

            // Draw the background.
            spriteBatch.Draw(background, mainFrame, Color.White);

            // Draw Pigs
            for (int i = 0; i < pigs.Length; i++)
            {
                spriteBatch.Draw(pigs[i], new Rectangle(mainFrame.Width - (1 + i) * 100, 880, 50, 50), Color.White);
            }

            Vector2 vogelfassungPosition = birdPosition.X <= 160 ? new Vector2(birdPosition.X + 25, birdPosition.Y + 25) : new Vector2(initialBirdPosition.X + 25, initialBirdPosition.Y + 25);
            // Draw hinteres Gummi
            DrawLine(spriteBatch, new Vector2(rechterPfosten.X, rechterPfosten.Y), vogelfassungPosition, 5);
            // Draw Flitsche
            spriteBatch.Draw(flitsche, new Rectangle(flitschePosition.X, flitschePosition.Y, 220, 220), Color.White);
            // Draw Bird
            spriteBatch.Draw(bird, new Rectangle(birdPosition.X, birdPosition.Y, 50, 50), Color.White);
            // Calculate Vogelfassung
            Vector2 flitscheToVogel = new Vector2(vogelfassungPosition.X-25 - linkerPfosten.X, vogelfassungPosition.Y-25 - linkerPfosten.Y);
            Vector2 vogelFassungDirection = new Vector2(1.0f, -flitscheToVogel.X / flitscheToVogel.Y);
            vogelFassungDirection.Normalize();
            vogelFassungDirection = vogelFassungDirection * 15;
            vogelFassungDirection = linkerPfosten.Y > vogelfassungPosition.Y ? -vogelFassungDirection : vogelFassungDirection;
            // Draw Vogelfassung
            DrawLine(spriteBatch, vogelfassungPosition - vogelFassungDirection, vogelfassungPosition + vogelFassungDirection, 17);
            // Draw vorderes Gummi
            DrawLine(spriteBatch, new Vector2(linkerPfosten.X, linkerPfosten.Y), vogelfassungPosition, 5);

            // End building the sprite.
            spriteBatch.End();

            spriteBatch.Begin();
            // Draw Pointer
            spriteBatch.Draw(pointerState == PointerState.PointerOpen ? handOpen : handClosed, new Rectangle(pointerPosition.X - 50, pointerPosition.Y - 50, 100, 100), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }

        void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, int thickness)
        {
            Vector2 edge = end - start;
            // calculate angle to rotate line
            float angle =
                (float)Math.Atan2(edge.Y, edge.X);


            sb.Draw(gummi,
                new Rectangle(// rectangle defines shape of line and position of start of line
                    (int)start.X,
                    (int)start.Y,
                    (int)edge.Length(), //sb will strech the texture to fill this rectangle
                    thickness), //width of line, change this to make thicker line
                null,
                new Color(76, 34, 22), //colour of line
                angle,     //angle of line (calulated above)
                new Vector2(0, 0), // point in line about which to rotate
                SpriteEffects.None,
                0);
        }

        public void grabBird(Point position)
        {
            if (currentState != GameState.Flying)
            {
                birdPosition = transform(position);
                currentState = GameState.Firing;
                buildCurve(birdPosition);
            }
        }

        public void moveBird(Point toPosition)
        {
            if (currentState != GameState.Flying)
            {
                birdPosition = transform(toPosition);
                buildCurve(birdPosition);
            }
        }

        public void releaseBird(Point position)
        {
            if (currentState != GameState.Flying)
            {
                birdPosition = transform(position);
                buildCurve(birdPosition);
                currentState = GameState.Flying;
            }
        }

        private void buildCurve(Point birdPosition)
        {
            double x1 = birdPosition.X, x2 = initialBirdPosition.X, y1 = birdPosition.Y, y2 = initialBirdPosition.Y;
            double m = (double)(y2 - y1) / (double)(x2 == x1 ? 1 : x2 - x1);
            a = m / 2 / (x1 - (x2 - x1) * 5.0d);
            b = m - 2 * a * x1;
            c = y1 - a * Math.Pow(x1, 2) - b * x1;

            t0 = System.Environment.TickCount;
            x0 = x1;

            System.Console.WriteLine(("y = " + a + " * x^2  + " + b + " * x + " + c).Replace(',', '.'));
            //System.Console.WriteLine("y = " + b + " * (" + t0 + "- t) + " + x0);
            System.Console.WriteLine("m = " + m);
        }

        public void scroll(Vector2 moveTo)
        {
            cameraPoint = new Point(cameraPoint.X + (int)(moveTo.X), cameraPoint.Y + (int)(moveTo.Y));
        }

        public Point currentBirdPosition()
        {
            return backTransform(this.birdPosition);
        }

        public void zoom(float zoomLevel, Point toPoint)
        {
            Point backTransformedCenter = backTransform(new Point(mainFrame.Width / 2, mainFrame.Height / 2));
            cameraPoint = backTransform(new Point(toPoint.X - backTransformedCenter.X, toPoint.Y - backTransformedCenter.Y));
            this.zoomLevel = zoomLevel;
        }

        public void action()
        {
        }

        public void updatePointer(Point point, PointerState pointerState)
        {
            pointerPosition = point;
            this.pointerState = pointerState;
        }

        public Point backTransform(Point toTransform)
        {
            return new Point(cameraPoint.X + (int)(toTransform.X * zoomLevel), cameraPoint.Y + (int)(toTransform.Y * zoomLevel));
        }

        public Point transform(Point toTransform)
        {
            return new Point((int)((toTransform.X - cameraPoint.X) / zoomLevel), (int)((toTransform.Y - cameraPoint.Y) / zoomLevel));
        }
    }
}
