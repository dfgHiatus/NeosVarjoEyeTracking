using static Neos_Varjo_Eye.VarjoCompanionInterface;
using System;
using System.Threading;
using BaseX;

namespace Neos_Varjo_Eye
{
    // This class contains the overrides for any VRCFT Tracking Data struct functions
    public static class TrackingData
    {
        // This function parses the external module's full-data data into multiple VRCFT-Parseable single-eye structs
        public static void Update(MemoryData external)
        {
            Neos_Varjo_Eye_Integration.memoryData.rightEye = external.rightEye;
            Neos_Varjo_Eye_Integration.memoryData.leftEye = external.leftEye;
            Neos_Varjo_Eye_Integration.memoryData.combined = external.combined;
        }
    }
    
    public class VarjoTrackingModule
    {
        public static readonly VarjoCompanionInterface tracker = new VarjoCompanionInterface();
        private static CancellationTokenSource _cancellationToken;

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            // MelonLogger.Msg("Initializing Varjo module");
            bool pipeConnected = tracker.ConnectToPipe();
            return (pipeConnected, false);
        }

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public Action GetUpdateThreadFunc()
        {
            _cancellationToken = new CancellationTokenSource();
            return () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    Update();
                    Thread.Sleep(10);
                }
            };
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        public void Update()
        {
            tracker.Update();
        }

        // A chance to de-initialize everything. This runs synchronously inside main game thread. Do not touch any Unity objects here.
        public void Teardown()
        {
            _cancellationToken.Cancel();
            tracker.Teardown();
            _cancellationToken.Dispose();
            // MelonLogger.Msg("Teardown");
        }

        public bool SupportsEye => true;
        public bool SupportsLip => false;
    }
}