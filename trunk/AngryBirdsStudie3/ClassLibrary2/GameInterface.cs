using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Interfaces
{
    public enum GameState { GameStart, Firing, BirdFlying, BirdFlyingActivated };

    public struct Pointer
    {
        public enum PointerState { PointerClosed, PointerOpen };
        public enum PointerType { HandLeft, HandRight };
        public Point point;
        public PointerType type;
        public PointerState state;
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
    }
}
