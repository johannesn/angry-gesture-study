using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Interfaces;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace Maussteuerung
{
    public class MouseInterface
    {
        int scrollWheelValue = 0;
        float zoomLevel = 1.0f;
        Point lastMousePosition;
        int lastMouseClickTime;

        enum InterfaceState { Scrolling, Firing, Idle };
        InterfaceState state = InterfaceState.Idle;

        GameInterface GameInterface
        {
            get;
            set;
        }
        
        public MouseInterface(GameInterface gameInterface)
        {
            GameInterface = gameInterface;
            scrollWheelValue = Mouse.GetState().ScrollWheelValue;
        }

        public void updateMouseState(MouseState mouseState)
        {
            Pointer pointer = new Pointer();
            pointer.point = new Point(mouseState.X, mouseState.Y);
            pointer.type = Pointer.PointerType.HandRight;
            pointer.state = mouseState.LeftButton == ButtonState.Pressed ? Pointer.PointerState.PointerClosed : Pointer.PointerState.PointerOpen;
            GameInterface.updatePointer(new Pointer[]{pointer});

            if (scrollWheelValue != mouseState.ScrollWheelValue)
            {
                zoomLevel = Math.Max(Math.Min(2.0f, (float)(mouseState.ScrollWheelValue - scrollWheelValue) / 120.0f * 0.1f + zoomLevel), 1.0f);
                GameInterface.zoom(zoomLevel, new Point(mouseState.X, mouseState.Y));
                scrollWheelValue = mouseState.ScrollWheelValue;
            }

            if (state == InterfaceState.Idle)
            {
                if (mouseState.LeftButton.Equals(ButtonState.Pressed))
                {
                    if (Math.Abs(GameInterface.currentBirdPosition().X - mouseState.X) <= 50 &&
                        Math.Abs(GameInterface.currentBirdPosition().Y - mouseState.Y) <= 50)
                    {
                        GameInterface.grabBird(new Point(mouseState.X, mouseState.Y));
                        state = InterfaceState.Firing;
                    }
                    else
                    {
                        state = InterfaceState.Scrolling;
                    }
                    lastMouseClickTime = Environment.TickCount;
                }

            }
            else if (mouseState.LeftButton.Equals(ButtonState.Released))
            {
                if (state == InterfaceState.Firing)
                {
                    GameInterface.releaseBird(new Point(mouseState.X, mouseState.Y));
                }
                else if(state != InterfaceState.Firing && Environment.TickCount - lastMouseClickTime < 500)
                {
                    GameInterface.action();
                }
                else if (state == InterfaceState.Scrolling)
                {
                    GameInterface.scroll(new Vector2(mouseState.X - lastMousePosition.X, mouseState.Y - lastMousePosition.Y));
                }
                state = InterfaceState.Idle;
            }
            else if (mouseState.LeftButton.Equals(ButtonState.Pressed))
            {
                if (state == InterfaceState.Firing)
                {
                    GameInterface.moveBird(new Point(mouseState.X, mouseState.Y));
                }
                else if (state == InterfaceState.Scrolling)
                {
                    GameInterface.scroll(new Vector2(mouseState.X - lastMousePosition.X, mouseState.Y - lastMousePosition.Y));
                }
            }

            lastMousePosition = new Point(mouseState.X, mouseState.Y);
        }
    }
}
