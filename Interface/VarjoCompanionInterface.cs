using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using FrooxEngine;

namespace Neos_Varjo_Eye
{
    // a dumb memory structure to copy all the data between the mod and the companion
    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryEye
    {
        public bool opened;
        public double pupilSize;
        public double x;
        public double y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryData
    {
        public bool shutdown;
        public bool calibrated;
        public MemoryEye leftEye;
        public MemoryEye rightEye;
        public MemoryEye combined;
    }

    public class VarjoCompanionInterface
    {
        private MemoryMappedFile MemMapFile;
        private MemoryMappedViewAccessor ViewAccessor;
        public MemoryData memoryGazeData;
        private Process CompanionProcess;

        public bool ConnectToPipe()
        {
            if (!VarjoAvailable())
            {
                // Warn("Varjo headset isn't detected");
                return false;
            }
            var modDir = Path.Combine(Engine.Current.AppPath, "nml_mods");

            CompanionProcess = new Process();
            CompanionProcess.StartInfo.WorkingDirectory = modDir;
            CompanionProcess.StartInfo.FileName = Path.Combine(modDir, "VarjoCompanion.exe");
            CompanionProcess.Start();

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    MemMapFile = MemoryMappedFile.OpenExisting("VarjoEyeTracking");
                    ViewAccessor = MemMapFile.CreateViewAccessor();
                    return true;
                }
                catch (FileNotFoundException)
                {
                    // Warn("VarjoEyeTracking mapped file doesn't exist; the companion app probably isn't running");
                }
                catch (Exception ex)
                {
                    // Error("Could not open the mapped file: " + ex);
                    return false;
                }
                Thread.Sleep(500);
            }

            return false;
        }

        public void Update()
        {
            if (MemMapFile == null) return;
            ViewAccessor.Read(0, out memoryGazeData);
        }

        public void Teardown()
        {
            if (MemMapFile == null) return;
            memoryGazeData.shutdown = true; // tell the companion app to shut down gracefully but it doesn't work anyway
            ViewAccessor.Write(0, ref memoryGazeData);
            MemMapFile.Dispose();
            CompanionProcess.Close();
        }

        private bool VarjoAvailable()
        {
            // totally not now the official Varjo library works under the hood
            return File.Exists("\\\\.\\pipe\\Varjo\\InfoService");
        }
    }
}