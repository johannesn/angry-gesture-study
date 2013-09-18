using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Interfaces
{
    public enum GameState { GameStart, Firing, BirdFlying, BirdFlyingActivated };

    public struct Pointer
    {
        public enum PointerState { PointerClosed, PointerOpen, PointerPress };
        public enum PointerType { HandLeft = 0, HandRight = 1};
        public Point point;
        public PointerType type;
        public PointerState state;
        public double pressExtend;
    }

    public interface GameInterface
    {
        void grabBird(Point position);

        void moveBird(Point toPosition);

        void releaseBird(Point position);

        Point currentBirdPosition();

        void scroll(Vector2 moveTo);

        void zoom(float zoomLevel, Vector2 moveTo);

        void action();

        void updatePointer(Pointer[] pointer);

        void ResetForNewUser();

        Color[] getPlayer_background();

        void setPlayer_background(Color[] background);

        void PlayerLeft();

        void PlayerEntered();

        Rectangle getDimensions();
    }
}
