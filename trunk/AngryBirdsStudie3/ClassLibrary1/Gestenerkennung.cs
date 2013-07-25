using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Interaction;
using Microsoft.Xna.Framework;
using Interfaces;

namespace Gestenerkennung
{
    public class DummyInteractionClient : IInteractionClient
    {
        public InteractionInfo GetInteractionInfoAtLocation(
            int skeletonTrackingId,
            InteractionHandType handType,
            double x,
            double y)
        {
            var result = new InteractionInfo();
            result.IsGripTarget = true;
            result.IsPressTarget = true;
            result.PressAttractionPointX = 0.5;
            result.PressAttractionPointY = 0.5;
            result.PressTargetControlId = 1;

            return result;
        }
    }

    public class GestenInterface
    {
        private KinectSensor kinect;
        private InteractionStream interactionStream;
        private Skeleton[] skeletonData;
        private UserInfo[] userInfos; //the information about the interactive users
        private Dictionary<int, InteractionHandEventType> _lastLeftHandEvents = new Dictionary<int, InteractionHandEventType>();
        private Dictionary<int, InteractionHandEventType> _lastRightHandEvents = new Dictionary<int, InteractionHandEventType>();
        private InterfaceState interfaceState = InterfaceState.Idle;
        private enum InterfaceState { Idle, Firing, Scrolling, Zooming };
        private Pointer.PointerType activeHand;
        private int activityTime;
        private float zoomLevel = 1.0f;
        private Dictionary<Pointer.PointerType, Point> scrollingAnchor;
        private Dictionary<Pointer.PointerType, double> backMovement;
        private Dictionary<Pointer.PointerType, bool> pressed;
        private Dictionary<Pointer.PointerType, Vector3> previousHandPosition;

        public GameInterface GameInterface
        {
            get;
            set;
        }

        public GestenInterface(GameInterface gameInterface)
        {
            GameInterface = gameInterface;
            scrollingAnchor = new Dictionary<Pointer.PointerType, Point>();
            pressed = new Dictionary<Pointer.PointerType, bool>();
            pressed[Pointer.PointerType.HandRight] = false;
            pressed[Pointer.PointerType.HandLeft] = false;
            backMovement = new Dictionary<Pointer.PointerType, double>();
            previousHandPosition = new Dictionary<Pointer.PointerType, Vector3>();


            kinect = KinectSensor.KinectSensors.FirstOrDefault();
            if (kinect == null)
            {
                return;
            }

            skeletonData = new Skeleton[kinect.SkeletonStream.FrameSkeletonArrayLength];
            userInfos = new UserInfo[InteractionFrame.UserInfoArrayLength];


            //kinect.DepthStream.Range = DepthRange.Near;
            kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

            kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
            //kinect.SkeletonStream.EnableTrackingInNearRange = true;
            kinect.SkeletonStream.Enable();

            interactionStream = new InteractionStream(kinect, new DummyInteractionClient());
            interactionStream.InteractionFrameReady += InteractionStreamOnInteractionFrameReady;

            kinect.DepthFrameReady += SensorOnDepthFrameReady;
            kinect.SkeletonFrameReady += SensorOnSkeletonFrameReady;

            kinect.Start(); // Start Kinect sensor
        }

        private void SensorOnSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                    return;

                try
                {
                    skeletonFrame.CopySkeletonDataTo(skeletonData);
                    var accelerometerReading = kinect.AccelerometerGetCurrentReading();
                    interactionStream.ProcessSkeleton(skeletonData, accelerometerReading, skeletonFrame.Timestamp);
                }
                catch (InvalidOperationException)
                {
                    // SkeletonFrame functions may throw when the sensor gets
                    // into a bad state.  Ignore the frame in that case.
                }
            }
        }

        private void SensorOnDepthFrameReady(object sender, DepthImageFrameReadyEventArgs depthImageFrameReadyEventArgs)
        {
            using (DepthImageFrame depthFrame = depthImageFrameReadyEventArgs.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                    return;

                try
                {
                    interactionStream.ProcessDepth(depthFrame.GetRawPixelData(), depthFrame.Timestamp);
                }
                catch (InvalidOperationException)
                {
                    // DepthFrame functions may throw when the sensor gets
                    // into a bad state.  Ignore the frame in that case.
                }
            }
        }

        private void InteractionStreamOnInteractionFrameReady(object sender, InteractionFrameReadyEventArgs args)
        {
            using (var iaf = args.OpenInteractionFrame()) //dispose as soon as possible
            {
                if (iaf == null)
                    return;

                iaf.CopyInteractionDataTo(userInfos);
            }

            foreach (var userInfo in userInfos)
            {
                var userID = userInfo.SkeletonTrackingId;
                if (userID == 0)
                    continue;

                var hands = userInfo.HandPointers;
                if (hands.Count > 0)
                {
                    Dictionary<Pointer.PointerType, Pointer> pointer = new Dictionary<Pointer.PointerType, Pointer>();
                    Pointer leftPointer, rightPointer;
                    InteractionHandPointer leftHand, rightHand;
                    foreach (var hand in hands)
                    {
                        var lastHandEvents = hand.HandType == InteractionHandType.Left
                                                 ? _lastLeftHandEvents
                                                 : _lastRightHandEvents;

                        if (hand.IsTracked && hand.IsActive && !hand.HandType.Equals(InteractionHandType.None))
                        {
                            if (hand.HandEventType != InteractionHandEventType.None)
                                lastHandEvents[userID] = hand.HandEventType;

                            var lastHandEvent = lastHandEvents.ContainsKey(userID)
                                                ? lastHandEvents[userID]
                                                : InteractionHandEventType.None;

                            Pointer p = new Pointer();
                            p.point = hand.HandType.Equals(InteractionHandType.Left) ? new Point((int)(hand.X * 1920.0d / 2.0d), (int)(hand.Y * 1080.0d))
                                : new Point((int)(hand.X * 1920.0d / 2.0d) + 1920 / 2, (int)(hand.Y * 1080.0d));
                            p.type = hand.HandType.Equals(InteractionHandType.Left) ? Pointer.PointerType.HandLeft : Pointer.PointerType.HandRight;

                            if (hand.IsPressed)
                            {
                                pressed[p.type] = true;
                                previousHandPosition[p.type] = new Vector3((float)hand.RawX, (float)hand.RawY, (float)hand.RawZ);
                                backMovement[p.type] = 0;
                            }
                            else if (pressed[p.type] && previousHandPosition.ContainsKey(p.type))
                            {
                                Vector3 oldPosition = previousHandPosition[p.type];
                                Vector3 newPosition = new Vector3((float)hand.RawX, (float)hand.RawY, (float)hand.RawZ);
                                Vector3 movement = newPosition - oldPosition;
                                double moveZ = movement.Z;
                                //System.Console.Write(movement);
                                movement.Normalize();
                                double angle = Math.Acos(movement.Z);
                                if ((angle <= Math.PI / 4.0d || angle >= 3.0d * Math.PI / 4.0d) && moveZ < 0)
                                {
                                    backMovement[p.type] += moveZ;
                                    if (backMovement[p.type] <= 0.2d)
                                    {
                                        pressed[p.type] = false;
                                        previousHandPosition.Remove(p.type);
                                    }
                                }
                                else
                                {
                                    backMovement[p.type] = 0.0d;
                                }
                                //System.Console.WriteLine();
                            }

                            p.pressExtend = pressed[p.type] ? 1.0d : Math.Max(Math.Min(hand.PressExtent, 1.0d), 0.0d);

                            p.state = hand.HandEventType.Equals(InteractionHandEventType.Grip) || lastHandEvent.Equals(InteractionHandEventType.Grip) ? Pointer.PointerState.PointerClosed
                                : pressed[p.type] ? Pointer.PointerState.PointerPress : Pointer.PointerState.PointerOpen;

                            pointer.Add(p.type, p);

                            if (p.type == Pointer.PointerType.HandLeft)
                            {
                                leftPointer = p;
                                leftHand = hand;
                            }
                            else if (p.type == Pointer.PointerType.HandRight)
                            {
                                rightPointer = p;
                                rightHand = hand;
                            }
                        }
                    }

                    GameInterface.updatePointer(pointer.Values.ToArray());

                    if (interfaceState == InterfaceState.Firing)
                    {
                        if (pointer[activeHand].state == Pointer.PointerState.PointerOpen)
                        {
                            GameInterface.releaseBird(pointer[activeHand].point);
                            interfaceState = InterfaceState.Idle;
                        }
                        else
                        {
                            GameInterface.moveBird(pointer[activeHand].point);
                        }
                    }
                    else
                    {
                        switch (interfaceState)
                        {
                            case InterfaceState.Idle:
                                if (isOneClosedPointer(pointer))
                                {
                                    activeHand = getClosedPointer(pointer);

                                    if (Math.Abs(GameInterface.currentBirdPosition().X - pointer[activeHand].point.X) <= 150 * zoomLevel
                                        && Math.Abs(GameInterface.currentBirdPosition().Y - pointer[activeHand].point.Y) <= 150 * zoomLevel)
                                    {
                                        GameInterface.grabBird(pointer[activeHand].point);
                                        interfaceState = InterfaceState.Firing;
                                    }
                                    else
                                    {
                                        interfaceState = InterfaceState.Scrolling;
                                        scrollingAnchor[activeHand] = pointer[activeHand].point;
                                        activityTime = System.Environment.TickCount;
                                    }
                                }
                                else if (isTwoClosedPointer(pointer))
                                {
                                    interfaceState = InterfaceState.Zooming;
                                    scrollingAnchor[Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                                    scrollingAnchor[Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
                                }
                                break;
                            case InterfaceState.Scrolling:
                                if (isOneClosedPointer(pointer))
                                {
                                    if (pointer[activeHand].state == Pointer.PointerState.PointerOpen)
                                    {
                                        interfaceState = InterfaceState.Idle;
                                    }
                                    else
                                    {
                                        GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[activeHand].Y));
                                        scrollingAnchor[activeHand] = pointer[activeHand].point;
                                    }
                                }
                                else if (isTwoClosedPointer(pointer))
                                {
                                    GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[activeHand].Y));
                                    interfaceState = InterfaceState.Zooming;
                                    scrollingAnchor[Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                                    scrollingAnchor[Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
                                }
                                else
                                {
                                    interfaceState = InterfaceState.Idle;
                                    activityTime = System.Environment.TickCount - activityTime;

                                    if (activityTime >= 100 && activityTime <= 1000)
                                    {
                                        GameInterface.action();
                                    }
                                }
                                break;
                            case InterfaceState.Zooming:
                                if (isOneClosedPointer(pointer))
                                {
                                    activeHand = getClosedPointer(pointer);
                                    GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[activeHand].Y));
                                    scrollingAnchor[activeHand] = pointer[activeHand].point;
                                    interfaceState = InterfaceState.Scrolling;
                                }
                                else if (isTwoClosedPointer(pointer))
                                {
                                    Vector2 oldLeftToRightHand = new Vector2(scrollingAnchor[Pointer.PointerType.HandLeft].X - scrollingAnchor[Pointer.PointerType.HandRight].X,
                                        scrollingAnchor[Pointer.PointerType.HandLeft].Y - scrollingAnchor[Pointer.PointerType.HandRight].Y);
                                    Vector2 leftToRightHand = new Vector2(pointer[Pointer.PointerType.HandLeft].point.X - pointer[Pointer.PointerType.HandRight].point.X,
                                        pointer[Pointer.PointerType.HandLeft].point.Y - pointer[Pointer.PointerType.HandRight].point.Y);

                                    zoomLevel = (float)Math.Max(Math.Min((leftToRightHand.Length() - oldLeftToRightHand.Length()) / 2000.0f + zoomLevel, 2.0f), 1.0f);

                                    GameInterface.zoom(zoomLevel, leftToRightHand / 2 - oldLeftToRightHand / 2);

                                    scrollingAnchor[Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                                    scrollingAnchor[Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
                                }
                                else
                                {
                                    interfaceState = InterfaceState.Idle;
                                }
                                break;
                        }
                    }
                }
            }
        }

        private static Pointer.PointerType getClosedPointer(Dictionary<Pointer.PointerType, Pointer> pointer)
        {
            Pointer.PointerType closedHand = Pointer.PointerType.HandLeft;
            if (pointer.Keys.Contains(Pointer.PointerType.HandLeft) && pointer[Pointer.PointerType.HandLeft].state != Pointer.PointerState.PointerOpen)
            {
                closedHand = Pointer.PointerType.HandLeft;
            }
            else if (pointer.Keys.Contains(Pointer.PointerType.HandRight) && pointer[Pointer.PointerType.HandRight].state != Pointer.PointerState.PointerOpen)
            {
                closedHand = Pointer.PointerType.HandRight;
            }
            return closedHand;
        }

        private static bool isTwoClosedPointer(Dictionary<Pointer.PointerType, Pointer> pointer)
        {
            if (pointer.Keys.Contains(Pointer.PointerType.HandLeft) && pointer.Keys.Contains(Pointer.PointerType.HandRight))
            {
                return pointer[Pointer.PointerType.HandLeft].state != Pointer.PointerState.PointerOpen && pointer[Pointer.PointerType.HandLeft].state != Pointer.PointerState.PointerOpen;
            }
            else
            {
                return false;
            }
        }

        private static bool isOneClosedPointer(Dictionary<Pointer.PointerType, Pointer> pointer)
        {
            if (pointer.Keys.Contains(Pointer.PointerType.HandLeft) && pointer.Keys.Contains(Pointer.PointerType.HandRight))
            {
                return pointer[Pointer.PointerType.HandLeft].state != Pointer.PointerState.PointerOpen ^ pointer[Pointer.PointerType.HandRight].state != Pointer.PointerState.PointerOpen;
            }
            else if (pointer.Keys.Contains(Pointer.PointerType.HandLeft))
            {
                return pointer[Pointer.PointerType.HandLeft].state != Pointer.PointerState.PointerOpen;
            }
            else if (pointer.Keys.Contains(Pointer.PointerType.HandRight))
            {
                return pointer[Pointer.PointerType.HandRight].state != Pointer.PointerState.PointerOpen;
            }
            else
            {
                return false;
            }
        }

    }
}
