using System;
using System.Threading;

namespace NeosVarjoEye
{
    public class VarjoTrackingModule
    {
        public static readonly VarjoCompanionInterface tracker = new VarjoCompanionInterface();
        private static CancellationTokenSource _cancellationToken;

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public bool Initialize()
        {
            return tracker.ConnectToPipe();
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
        }
    }
}