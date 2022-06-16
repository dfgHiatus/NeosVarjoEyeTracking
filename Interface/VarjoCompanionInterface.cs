using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using FrooxEngine;
using BaseX;

namespace VarjoInterface.Companion
{
    public class VarjoCompanionInterface : VarjoModule
    {
        public MemoryMappedFile MemMapFile;
        public MemoryMappedViewAccessor ViewAccessor;
        public Process CompanionProcess;

        public override bool Initialize()
        {
            if (!VarjoAvailable())
            {
                UniLog.Log("Varjo headset isn't detected");
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
                    UniLog.Log("Connected to the Varjo Companion App!");
                    return true;
                }
                catch (FileNotFoundException)
                {
                    UniLog.Log($"Trying to connect to the Varjo Companion App. Attempt {i}/5");
                }
                catch (Exception ex)
                {
                    UniLog.Log("Could not open the mapped file: " + ex);
                    return false;
                }
                Thread.Sleep(500);
            }

            return false;
        }

        public override void Update()
        {
            if (MemMapFile == null) return;
            ViewAccessor.Read(0, out varjoData);
        }

        public override void Teardown()
        {
            if (MemMapFile == null) return;
            ViewAccessor.Write(0, ref varjoData);
            MemMapFile.Dispose();
            CompanionProcess.Close();
            // CompanionProcess.Kill(); might be better suited here
        }

        public override string GetName()
        {
            return "Companion Interface";
        }
    }
}