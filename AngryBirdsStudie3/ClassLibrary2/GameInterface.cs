using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Interfaces
{
    public enum PointerState { PointerClosed, PointerOpen };

    public enum GameState { GameStart, Firing, BirdFlying, BirdFlyingActivated };

    public interface GameInterface
    {
        void grabBird(Point position);

        void moveBird(Point toPosition);

        void releaseBird(Point position);

        Point currentBirdPosition();

        void scroll(Vector2 moveTo);

        void zoom(float zoomLevel, Point toPoint);

        void action();

        void updatePointer(Point point, PointerState pointerState);
    }
}
