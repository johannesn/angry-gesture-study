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


        public GameInterface GameInterface
        {
            get;
            set;
        }

        public GestenInterface(GameInterface gameInterface)
        {
            GameInterface = gameInterface;

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
                    List<Pointer> pointer = new List<Pointer>();
                    foreach (var hand in hands)
                    {
                        var lastHandEvents = hand.HandType == InteractionHandType.Left
                                                 ? _lastLeftHandEvents
                                                 : _lastRightHandEvents;

                        if (hand.IsTracked && !hand.HandType.Equals(InteractionHandType.None))
                        {
                            if (hand.HandEventType != InteractionHandEventType.None)
                            lastHandEvents[userID] = hand.HandEventType;

                            var lastHandEvent = lastHandEvents.ContainsKey(userID)
                                                ? lastHandEvents[userID]
                                                : InteractionHandEventType.None;

                            Pointer p = new Pointer();
                            p.point = hand.HandType.Equals(InteractionHandType.Left) ? new Point((int)(hand.X * 1920.0d / 2.0d), (int)(hand.Y * 1080.0d / 2.0d)) 
                                : new Point((int)(hand.X * 1920.0d / 2.0d) + 1920 / 2, (int)(hand.Y * 1080.0d / 2.0d));
                            p.type = hand.HandType.Equals(InteractionHandType.Left) ? Pointer.PointerType.HandLeft : Pointer.PointerType.HandRight;
                            p.state = hand.IsPressed || hand.HandEventType.Equals(InteractionHandEventType.Grip) || lastHandEvent.Equals(InteractionHandEventType.Grip) ? Pointer.PointerState.PointerClosed : Pointer.PointerState.PointerOpen;
                            pointer.Add(p);
                        }
                    }

                    GameInterface.updatePointer(pointer.ToArray());
                }
            }
        }

    }
}
