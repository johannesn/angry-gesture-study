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
    struct Pig
    {
        public Texture2D texture;
        public Point position;
        public enum PigState { Alive, Dead };
        public PigState state;
    }

    struct Bird
    {
        public Texture2D texture;
        public Point position;
        public enum BirdState { Idle, Firing, Flying, Activated, Dead };
        public BirdState state;
        public BirdType type;
        public enum BirdType { Red, Yellow };
    }

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
        Bird[] birds;
        Pig[] pigs;
        Texture2D gummi;
        Texture2D handOpen;
        Texture2D handClosed;
        Texture2D gameOver;
        SpriteFont font;

        // Drawing Points
        Rectangle mainFrame;
        Point flitschePosition = new Point(50, 730);
        Point initialBirdPosition = new Point(135, 755);
        Point linkerPfosten = new Point(140, 780);
        Point rechterPfosten = new Point(180, 780);
        int groundLine = 880;
        enum InterfaceType { InterfaceGesture, InterfaceMouse };
        InterfaceType interfaceType = InterfaceType.InterfaceGesture;
        GestenInterface gameInterface;
        MouseInterface mouseInterface;

        // Pointer parameter
        Pointer[] pointer;

        // Zooming/Scrolling parameter
        Point cameraPoint = new Point(0, 0);
        Point firingPoint;
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
        enum GameState { Idle, Firing, Flying, GameOver };
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
            // Set the rectangle parameters.
            mainFrame = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            // Load the background content.
            background = Content.Load<Texture2D>("background");
            flitsche = Content.Load<Texture2D>("flitsche");
            handOpen = Content.Load<Texture2D>("hand_open");
            handClosed = Content.Load<Texture2D>("hand_closed");
            gameOver = Content.Load<Texture2D>("gameOverWindow");
            font = Content.Load<SpriteFont>("spriteFont1");
            gummi = new Texture2D(GraphicsDevice, 1, 1);
            gummi.SetData<Color>(
                new Color[] { new Color(135, 61, 39) });

            initialize();
        }

        private void initialize()
        {
            pointer = new Pointer[0];

            birds = new Bird[5];
            for (int i = 0; i < birds.Length; i++)
            {
                birds[i].texture = Content.Load<Texture2D>(i % 2 == 0 ? "bird" : "yellowbird");
                birds[i].type = i % 2 == 0 ? Bird.BirdType.Red : Bird.BirdType.Yellow;
                birds[i].state = Bird.BirdState.Idle;
                birds[i].position = i == 0 ? initialBirdPosition : new Point(135 + 50 * i, groundLine);
            }

            pigs = new Pig[10];
            for (int i = 0; i < pigs.Length; i++)
            {
                pigs[i].texture = Content.Load<Texture2D>("pig" + (i + 1));
                pigs[i].position = new Point(mainFrame.Width - (1 + i) * 100, groundLine);
                pigs[i].state = Pig.PigState.Alive;
            }

            currentState = GameState.Idle;

            cameraPoint = new Point(0, 0);
            zoomLevel = 1.0f;
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

            if (Keyboard.GetState().IsKeyDown(Keys.Enter))
            {
                initialize();
            }

            if (currentState == GameState.Flying)
            {
                double x = (initialBirdPosition.X - x0) * 10.0f * (System.Environment.TickCount - t0) / 1000.0d + x0;
                double y = a * Math.Pow(x, 2) + b * x + c;
                //System.Console.WriteLine("x = " + x + " ; y = " + y + " ; t = "+ (System.Environment.TickCount - t0));
                if (y >= groundLine)
                {
                    y = groundLine;
                }

                birds[0].position = new Point((int)x, (int)y);
                cameraPoint.X = (int)(1920 / 2 * zoomLevel - birds[0].position.X * zoomLevel);
                cameraPoint.Y = (int)(1080 / 2 * zoomLevel - birds[0].position.Y * zoomLevel);

                for (int i = 0; i < pigs.Length; i++)
                {
                    if (new Vector2(birds[0].position.X - pigs[i].position.X, birds[0].position.Y - pigs[i].position.Y).Length() < 40.0f)
                    {
                        pigs[i].state = Pig.PigState.Dead;
                    }
                }

                if (y >= groundLine && x >= 135)
                {
                    currentState = GameState.Idle;
                    if (birds.Length > 1)
                    {
                        Bird[] tmp = new Bird[birds.Length - 1];
                        Array.Copy(birds, 1, tmp, 0, birds.Length - 1);
                        birds = tmp;
                        birds[0].position = initialBirdPosition;
                        cameraPoint = firingPoint;
                    }
                    else
                    {
                        currentState = GameState.GameOver;
                        birds = new Bird[0];
                    }
                }
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
                if (pigs[i].state == Pig.PigState.Alive)
                {
                    spriteBatch.Draw(pigs[i].texture, new Rectangle(pigs[i].position.X, pigs[i].position.Y, 50, 50), Color.White);
                }
            }

            Vector2 vogelfassungPosition = birds.Length > 0 && birds[0].position.X < 160 ? new Vector2(birds[0].position.X + 25, birds[0].position.Y + 25) : new Vector2(initialBirdPosition.X + 25, initialBirdPosition.Y + 25);
            // Draw hinteres Gummi
            DrawLine(spriteBatch, new Vector2(rechterPfosten.X, rechterPfosten.Y), vogelfassungPosition, 5);
            // Draw Flitsche
            spriteBatch.Draw(flitsche, new Rectangle(flitschePosition.X, flitschePosition.Y, 220, 220), Color.White);
            // Draw Bird

            foreach (Bird bird in birds)
            {
                spriteBatch.Draw(bird.texture, new Rectangle(bird.position.X, bird.position.Y, 50, 50), Color.White);
            }

            // Calculate Vogelfassung
            Vector2 flitscheToVogel = new Vector2(vogelfassungPosition.X - 25 - linkerPfosten.X, vogelfassungPosition.Y - 25 - linkerPfosten.Y);
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
            foreach (Pointer p in this.pointer)
            {
                //Rectangle rectangle = pointerTypes[i]==PointerType.HandRight?new Rectangle(pointerPositions[i].X - 50, pointerPositions[i].Y - 50, 100, 100)
                //    : new Rectangle(pointerPositions[i].X - 50, pointerPositions[i].Y + 50, -100, 100);
                //spriteBatch.Draw(pointerStates[i] == PointerState.PointerOpen ? handOpen : handClosed, rectangle, Color.White);
                spriteBatch.Draw(p.state == Pointer.PointerState.PointerOpen ? handOpen : handClosed, new Rectangle(p.point.X - 50, p.point.Y - 50, 100, 100),
                    null, Color.White, 0.0f, new Vector2(0, 0), p.type == Pointer.PointerType.HandLeft ? SpriteEffects.FlipHorizontally : 0, 0);

            }
            if (currentState == GameState.GameOver)
            {
                int score = 0;
                foreach (Pig pig in pigs)
                {
                    if (pig.state == Pig.PigState.Dead)
                    {
                        score++;
                    }
                }
                string message = String.Format("GAME OVER!!!{0}Du hast {1} Schweine erwischt!{0}Druecke Eingabe um ein{0}neues Spiel zu starten!", Environment.NewLine, score);
                Vector2 FontOrigin = font.MeasureString(message) / 2;
                spriteBatch.Draw(gameOver, new Vector2(mainFrame.Width / 4, mainFrame.Height / 4), Color.White);
                spriteBatch.DrawString(font, message, new Vector2(mainFrame.Width / 2, mainFrame.Height / 2), Color.White,
                              0, FontOrigin, 1.0f, SpriteEffects.None, 0.5f);
            }
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
            if (currentState != GameState.Flying && birds.Length > 0)
            {
                birds[0].position = transform(position);
                currentState = GameState.Firing;
                buildCurve(birds[0].position);
            }
        }

        public void moveBird(Point toPosition)
        {
            if (currentState != GameState.Flying && birds.Length > 0)
            {
                birds[0].position = transform(toPosition);
                buildCurve(birds[0].position);
            }
        }

        public void releaseBird(Point position)
        {
            if (currentState != GameState.Flying && birds.Length > 0)
            {
                birds[0].position = transform(position);
                buildCurve(birds[0].position);
                firingPoint = cameraPoint;
                currentState = GameState.Flying;
            }
        }

        private void buildCurve(Point birdPosition)
        {
            double x1 = birdPosition.X, x2 = initialBirdPosition.X, y1 = birdPosition.Y, y2 = initialBirdPosition.Y;
            double m = (double)(y2 - y1) / (double)(x2 == x1 ? 1 : x2 - x1);
            a = Math.Abs(m / 2 / (x1 - (x2 - x1) * 5.0d));
            b = m - 2 * a * x1;
            c = y1 - a * Math.Pow(x1, 2) - b * x1;

            t0 = System.Environment.TickCount;
            x0 = x1;

            //System.Console.WriteLine(("y = " + a + " * x^2  + " + b + " * x + " + c).Replace(',', '.'));
            //System.Console.WriteLine("y = " + b + " * (" + t0 + "- t) + " + x0);
            //System.Console.WriteLine("m = " + m);
        }

        public void scroll(Vector2 moveTo)
        {
            cameraPoint = new Point(cameraPoint.X + (int)(moveTo.X), cameraPoint.Y + (int)(moveTo.Y));
        }

        public Point currentBirdPosition()
        {
            return birds.Length > 0 ? backTransform(birds[0].position) : new Point(Int32.MinValue, Int32.MinValue);
        }

        public void zoom(float zoomLevel, Vector2 moveTo)
        {
            scroll(moveTo / zoomLevel);
            this.zoomLevel = zoomLevel;
        }

        public void action()
        {
            if (currentState == GameState.Flying && birds.Length > 0)
            {
                if (birds[0].type == Bird.BirdType.Yellow)
                {
                    a = 0;
                    b = 1;
                    c = birds[0].position.Y - 1 * birds[0].position.X;
                }
            }
        }

        public void updatePointer(Pointer[] pointer)
        {
            this.pointer = pointer;
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
