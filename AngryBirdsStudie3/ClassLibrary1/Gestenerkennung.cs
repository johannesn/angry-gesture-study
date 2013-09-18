using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Interaction;
using Microsoft.Xna.Framework;
using Interfaces;
using System.Windows.Converters;
using Microsoft.Xna.Framework.Graphics;

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
        private ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;
        private DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;
        private InteractionStream interactionStream;
        private Skeleton[] skeletonData;
        private UserInfo[] userInfos; //the information about the interactive users
        private InterfaceState interfaceState;
        private Point[] scrollingAnchor;
        private double[] backMovement;
        private double[] angle;
        private bool[] pressed;
        private bool[] gripped;
        private Vector3[] previousHandPosition;

        private enum InterfaceState { Idle, Firing, Scrolling, Zooming };
        private Pointer.PointerType activeHand;
        private int activityTime;
        private float zoomLevel = 1.0f;
        private Queue<int> playerOrder;

        // Color variables
        private DepthImagePixel[] depthPixels;
        private byte[] colorPixels;
        private ColorImagePoint[] colorCoordinates;
        private int colorToDepthDivisor;
        private int depthWidth;
        private int depthHeight;
        private Rectangle mainFrame;

        public GameInterface GameInterface
        {
            get;
            set;
        }

        public GestenInterface(GameInterface gameInterface)
        {
            GameInterface = gameInterface;
            playerOrder = new Queue<int>();

            mainFrame = gameInterface.getDimensions();

            initialize();

            kinect = KinectSensor.KinectSensors.FirstOrDefault();
            if (kinect == null)
            {
                return;
            }

            skeletonData = new Skeleton[kinect.SkeletonStream.FrameSkeletonArrayLength];
            userInfos = new UserInfo[InteractionFrame.UserInfoArrayLength];

            //kinect.DepthStream.Range = DepthRange.Near;
            kinect.DepthStream.Enable(DepthFormat);

            this.depthWidth = kinect.DepthStream.FrameWidth;

            this.depthHeight = kinect.DepthStream.FrameHeight;

            kinect.ColorStream.Enable(ColorFormat);

            int colorWidth = kinect.ColorStream.FrameWidth;
            int colorHeight = kinect.ColorStream.FrameHeight;

            this.colorToDepthDivisor = colorWidth / this.depthWidth;

            kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
            //kinect.SkeletonStream.EnableTrackingInNearRange = true;
            kinect.SkeletonStream.Enable();

            // Allocate space to put the depth pixels we'll receive
            this.depthPixels = new DepthImagePixel[kinect.DepthStream.FramePixelDataLength];

            // Allocate space to put the color pixels we'll create
            this.colorPixels = new byte[kinect.ColorStream.FramePixelDataLength];

            this.colorCoordinates = new ColorImagePoint[kinect.DepthStream.FramePixelDataLength];

            interactionStream = new InteractionStream(kinect, new DummyInteractionClient());
            interactionStream.InteractionFrameReady += InteractionStreamOnInteractionFrameReady;

            // Add an event handler to be called whenever there is new depth frame data
            kinect.AllFramesReady += this.SensorAllFramesReady;

            kinect.Start(); // Start Kinect sensor
        }

        private void initialize()
        {
            scrollingAnchor = new Point[2];
            pressed = new bool[2];
            pressed[(int)Pointer.PointerType.HandRight] = false;
            pressed[(int)Pointer.PointerType.HandLeft] = false;
            gripped = new bool[2];
            gripped[(int)Pointer.PointerType.HandRight] = false;
            gripped[(int)Pointer.PointerType.HandLeft] = false;
            backMovement = new double[2];
            angle = new double[2];
            previousHandPosition = new Vector3[2];
            interfaceState = InterfaceState.Idle;
        }

        private void InteractionStreamOnInteractionFrameReady(object sender, InteractionFrameReadyEventArgs args)
        {
            using (var iaf = args.OpenInteractionFrame()) //dispose as soon as possible
            {
                if (iaf == null)
                    return;

                iaf.CopyInteractionDataTo(userInfos);
            }

            lock (playerOrder)
            {

                // Find first player who hasn't yet left
                while (playerOrder.Count > 0 && !userInfos.Any(i => i.SkeletonTrackingId == playerOrder.Peek()))
                {
                    playerOrder.Dequeue();
                    ResetForNewUser();

                    if (playerOrder.Count <= 0)
                    {
                        GameInterface.PlayerLeft();
                    }
                }

                // Iterate until current player found, add those that are not in the queue to the queue
                foreach (UserInfo userInfo in userInfos)
                {
                    var userID = userInfo.SkeletonTrackingId;
                    if (userID == 0)
                        continue;

                    if (!playerOrder.Contains(userID))
                    {
                        if (playerOrder.Count <= 0)
                        {
                            GameInterface.PlayerEntered();
                        }
                        playerOrder.Enqueue(userID);
                        System.Console.WriteLine(userID + " entered");
                    }

                    if (playerOrder.Count > 0 && userID == playerOrder.Peek())
                    {
                        var hands = userInfo.HandPointers;
                        if (hands.Count > 0)
                        {
                            Dictionary<Pointer.PointerType, Pointer> pointer = ExtractPointer(userID, hands);
                            // Update GUI
                            UpdateGUI(pointer);
                        }
                    }
                }
            }
        }

        private void ResetForNewUser()
        {
            GameInterface.ResetForNewUser();

            initialize();
        }

        private Dictionary<Pointer.PointerType, Pointer> ExtractPointer(int userID, System.Collections.ObjectModel.ReadOnlyCollection<InteractionHandPointer> hands)
        {
            Dictionary<Pointer.PointerType, Pointer> pointer = new Dictionary<Pointer.PointerType, Pointer>(2);
            Pointer leftPointer, rightPointer;
            InteractionHandPointer leftHand, rightHand;
            foreach (var hand in hands)
            {
                Pointer.PointerType pointerType = hand.HandType.Equals(InteractionHandType.Left) ? Pointer.PointerType.HandLeft : Pointer.PointerType.HandRight;
                if (hand.IsTracked && hand.IsActive && !hand.HandType.Equals(InteractionHandType.None)
                    || pressed[(int)pointerType] || gripped[(int)pointerType])
                {
                    Pointer p = new Pointer();
                    p.point = hand.HandType.Equals(InteractionHandType.Left) ? new Point((int)(hand.X * mainFrame.Width / 2.0d), (int)(hand.Y * mainFrame.Height))
                        : new Point((int)(hand.X * mainFrame.Width / 2.0d) + mainFrame.Width / 2, (int)(hand.Y * mainFrame.Height));
                    p.type = hand.HandType.Equals(InteractionHandType.Left) ? Pointer.PointerType.HandLeft : Pointer.PointerType.HandRight;

                    if (hand.IsPressed && !pressed[(int)p.type])
                    {
                        pressed[(int)p.type] = true;
                        previousHandPosition[(int)p.type] = new Vector3((float)hand.RawX, (float)hand.RawY, (float)hand.RawZ);
                        backMovement[(int)p.type] = 0.0d;
                        double a = getAngle(userID, p.type);
                        angle[(int)p.type] = a;
                    }
                    else if (pressed[(int)p.type] && previousHandPosition[(int)p.type] != Vector3.Zero)
                    {
                        Vector3 oldPosition = previousHandPosition[(int)p.type];
                        Vector3 newPosition = new Vector3((float)hand.RawX, (float)hand.RawY, (float)hand.RawZ);
                        Vector3 movement = newPosition - oldPosition;
                        double moveZ = movement.Z;
                        movement.Normalize();
                        double a = Math.Acos(movement.Z);
                        double b = angle[(int)p.type] - getAngle(userID, p.type);
                        //Console.Out.WriteLine(b);
                        if ((a <= Math.PI / 4.0d || a >= 3.0d * Math.PI / 4.0d) && moveZ < 0 || b > Math.PI / 4.0d)
                        {
                            backMovement[(int)p.type] += moveZ;
                            if (backMovement[(int)p.type] <= 0.2d || b > Math.PI / 4.0d)
                            {
                                pressed[(int)p.type] = false;
                                previousHandPosition[(int)p.type] = Vector3.Zero;
                            }
                        }
                        else
                        {
                            backMovement[(int)p.type] = 0.0d;
                        }
                    }

                    p.pressExtend = pressed[(int)p.type] ? 1.0d : Math.Max(Math.Min(hand.PressExtent, 1.0d), 0.0d);

                    if (hand.HandEventType.Equals(InteractionHandEventType.Grip))
                    {
                        gripped[(int)p.type] = true;
                    }
                    else if (hand.HandEventType.Equals(InteractionHandEventType.GripRelease))
                    {
                        gripped[(int)p.type] = false;
                    }
                    p.state = hand.HandEventType.Equals(InteractionHandEventType.Grip) || gripped[(int)p.type] ? Pointer.PointerState.PointerClosed
                        : pressed[(int)p.type] ? Pointer.PointerState.PointerPress : Pointer.PointerState.PointerOpen;

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
            return pointer;
        }

        private double getAngle(int userID, Pointer.PointerType p)
        {
            Skeleton s = skeletonData.First(i => i.TrackingId == userID);
            Joint elbow, wrist, sholder;
            if (p == Pointer.PointerType.HandLeft)
            {
                elbow = s.Joints[JointType.ElbowLeft];
                wrist = s.Joints[JointType.WristLeft];
                sholder = s.Joints[JointType.ShoulderLeft];
            }
            else
            {
                elbow = s.Joints[JointType.ElbowRight];
                wrist = s.Joints[JointType.WristRight];
                sholder = s.Joints[JointType.ShoulderRight];
            }
            Vector3 ellbowtohand = new Vector3(wrist.Position.X - elbow.Position.X, wrist.Position.Y - elbow.Position.Y, wrist.Position.Z - elbow.Position.Z);
            Vector3 ellbowtoshoulder = new Vector3(sholder.Position.X - elbow.Position.X, sholder.Position.Y - elbow.Position.Y, sholder.Position.Z - elbow.Position.Z);
            double a = Math.Acos((ellbowtohand.X * ellbowtoshoulder.X + ellbowtohand.Y * ellbowtoshoulder.Y + ellbowtohand.Z * ellbowtoshoulder.Z) / ellbowtohand.Length() / ellbowtoshoulder.Length());
            return a;
        }

        private void UpdateGUI(Dictionary<Pointer.PointerType, Pointer> pointer)
        {
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
                                scrollingAnchor[(int)activeHand] = pointer[activeHand].point;
                                activityTime = System.Environment.TickCount;
                            }
                        }
                        else if (isTwoClosedPointer(pointer))
                        {
                            interfaceState = InterfaceState.Zooming;
                            scrollingAnchor[(int)Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                            scrollingAnchor[(int)Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
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
                                GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[(int)activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[(int)activeHand].Y));
                                scrollingAnchor[(int)activeHand] = pointer[activeHand].point;
                            }
                        }
                        else if (isTwoClosedPointer(pointer))
                        {
                            GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[(int)activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[(int)activeHand].Y));
                            interfaceState = InterfaceState.Zooming;
                            scrollingAnchor[(int)Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                            scrollingAnchor[(int)Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
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
                            GameInterface.scroll(new Vector2(pointer[activeHand].point.X - scrollingAnchor[(int)activeHand].X, pointer[activeHand].point.Y - scrollingAnchor[(int)activeHand].Y));
                            scrollingAnchor[(int)activeHand] = pointer[activeHand].point;
                            interfaceState = InterfaceState.Scrolling;
                        }
                        else if (isTwoClosedPointer(pointer))
                        {
                            Vector2 oldLeftToRightHand = new Vector2(scrollingAnchor[(int)Pointer.PointerType.HandLeft].X - scrollingAnchor[(int)Pointer.PointerType.HandRight].X,
                                scrollingAnchor[(int)Pointer.PointerType.HandLeft].Y - scrollingAnchor[(int)Pointer.PointerType.HandRight].Y);
                            Vector2 leftToRightHand = new Vector2(pointer[Pointer.PointerType.HandLeft].point.X - pointer[Pointer.PointerType.HandRight].point.X,
                                pointer[Pointer.PointerType.HandLeft].point.Y - pointer[Pointer.PointerType.HandRight].point.Y);

                            zoomLevel = (float)Math.Max(Math.Min((leftToRightHand.Length() - oldLeftToRightHand.Length()) / 2000.0f + zoomLevel, 2.0f), 1.0f);

                            Vector2 oldMiddle = new Vector2(scrollingAnchor[(int)Pointer.PointerType.HandLeft].X, scrollingAnchor[(int)Pointer.PointerType.HandLeft].Y) + oldLeftToRightHand / 2;
                            Vector2 newMiddle = new Vector2(pointer[Pointer.PointerType.HandLeft].point.X, pointer[Pointer.PointerType.HandLeft].point.Y) + leftToRightHand / 2;
                            Vector2 movement = oldMiddle - newMiddle;

                            GameInterface.zoom(zoomLevel, movement);

                            scrollingAnchor[(int)Pointer.PointerType.HandRight] = pointer[Pointer.PointerType.HandRight].point;
                            scrollingAnchor[(int)Pointer.PointerType.HandLeft] = pointer[Pointer.PointerType.HandLeft].point;
                        }
                        else
                        {
                            interfaceState = InterfaceState.Idle;
                        }
                        break;
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

        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == kinect)
            {
                return;
            }
            
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    
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

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                }
            }

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

            lock (playerOrder)
            {
                foreach (Skeleton skeleton in skeletonData)
                {
                    if (skeleton.TrackingId != 0 && !playerOrder.Contains(skeleton.TrackingId))
                    {
                        if (playerOrder.Count <= 0)
                        {
                            GameInterface.PlayerEntered();
                        }
                        playerOrder.Enqueue(skeleton.TrackingId);
                    }
                }
            }
        }

        private int lastTime = -1;
        public Color[] getPlayerBackground()
        {
            //Console.Out.Write(System.Environment.TickCount - lastTime + " ");

            if (System.Environment.TickCount - lastTime > 50 || lastTime < 0)
            {
                lastTime = System.Environment.TickCount;
                //Dictionary<int, bool> output = new Dictionary<int,bool>();
                Color[] data = new Color[kinect.DepthStream.FramePixelDataLength];
                // do our processing outside of the using block
                // so that we return resources to the kinect as soon as possible
                if (null != depthPixels && null != colorCoordinates)
                {
                    kinect.CoordinateMapper.MapDepthFrameToColorFrame(
                        DepthFormat,
                        this.depthPixels,
                        ColorFormat,
                        this.colorCoordinates);

                    // loop over each row and column of the depth
                    for (int y = 0; y < this.depthHeight; ++y)
                    {
                        for (int x = 0; x < this.depthWidth; ++x)
                        {
                            // calculate index into depth array
                            int depthIndex = x + (y * this.depthWidth);

                            DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                            int player = depthPixel.PlayerIndex;

                            // players skeleton id
                            int skeletonID = (player - 1 >= 0 && player - 1 < skeletonData.Length) ? skeletonData[player - 1].TrackingId : -1;

                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            // make sure the depth pixel maps to a valid point in color space
                            // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                            // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                            // because of how the sensor works it is more correct to do it this way than to set to the right
                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {

                                // calculate index into the green screen pixel array
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * this.depthWidth);
                                // if we're tracking a player for the current pixel, do green screen
                                if (player > 0)
                                {
                                    int alpha = playerOrder.Count <= 0 || skeletonID == playerOrder.Peek() ? 255 : 125;
                                    //if (!output.ContainsKey(skeletonID))
                                    //{
                                    //    output[skeletonID] = true;
                                    //    System.Console.WriteLine(skeletonID + " found in image, first player is " + (playerOrder.Count > 0 ? "" + playerOrder.Peek() : "not there"));
                                    //}
                                    data[greenScreenIndex] = new Color(colorPixels[greenScreenIndex * 4 + 2], colorPixels[greenScreenIndex * 4 + 1], colorPixels[greenScreenIndex * 4], alpha);
                                    data[greenScreenIndex - 1] = new Color(colorPixels[(greenScreenIndex - 1) * 4 + 2], colorPixels[(greenScreenIndex - 1) * 4 + 1], colorPixels[(greenScreenIndex - 1) * 4], alpha);
                                }
                                else
                                {
                                    data[greenScreenIndex] = Color.Transparent;
                                }
                            }
                        }
                    }
                }

                //Console.WriteLine(System.Environment.TickCount - lastTime + "");

                return data;
            }
            
            //Console.WriteLine();
            return null;
        }

        public void stop()
        {
            kinect.Stop();
            this.kinect = null;
        }

        public void nextPlayer()
        {
            int firstPlayer = playerOrder.Dequeue();
            playerOrder.Enqueue(firstPlayer);

            GameInterface.PlayerEntered();
        }
    }
}
