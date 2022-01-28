using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Neos_Varjo_Eye
{
	public class Neos_Varjo_Eye_Integration : NeosMod
	{
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

		public static VarjoTracker varjoTracker;
		public static MemoryData varjoMemory;
		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/dfgHiatus/Neos-Eye-Face-API/";
		public override void OnEngineInit()
		{
			// Harmony.DEBUG = true;
			Harmony harmony = new Harmony("net.dfgHiatus.Neos-Eye-Face-API");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new[] { typeof(Engine) })]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					varjoTracker = new VarjoTracker();
					if (!varjoTracker.ConnectToPipe())
						throw new Exception();
					varjoMemory = varjoTracker.memoryGazeData;
					GenericInputDevice gen = new GenericInputDevice();
					Debug("Module Name: " + gen.ToString());
					__instance.RegisterInputDriver(gen);
				}
				catch (Exception e)
				{
					Warn("Module failed to initiallize.");
					Warn(e.ToString());
				}
			}
		}

		[HarmonyPatch(typeof(Engine), "Shutdown")]
		public class ShutdownPatch
		{
			public static bool Prefix()
			{
				varjoTracker.Teardown();
				return true;
			}
		}

		public class VarjoTracker
		{
			// Begin VarjoTracker.cs
			private MemoryMappedFile MemMapFile;
			private MemoryMappedViewAccessor ViewAccessor;
			public MemoryData memoryGazeData;
			private Process CompanionProcess;

			public bool ConnectToPipe()
			{
				if (!varjo_IsAvailable())
				{
					Warn("Varjo headset isn't detected");
					return false;
				}

				// TODO Get NML Mods folder and return the VarjoCompanion.exe path
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
						Warn("VarjoEyeTracking mapped file doesn't exist; the companion app probably isn't running");
					}
					catch (Exception ex)
					{
						Error("Could not open the mapped file: " + ex);
						return false;
					}
					Thread.Sleep(500);
				}

				return false;
			}

			public void InterfaceUpdate()
			{
				if (MemMapFile == null) return;
				ViewAccessor.Read(0, out memoryGazeData);
			}

			public void InterfaceTeardown()
			{
				if (MemMapFile == null) return;
				memoryGazeData.shutdown = true;
				ViewAccessor.Write(0, ref memoryGazeData);
				MemMapFile.Dispose();
				CompanionProcess.Close();
			}

			[DllImport("VarjoLib", CharSet = CharSet.Auto)]
			public static extern bool varjo_IsAvailable();
			// End VarjoTracker.cs

			// Start VarjoTrackingInterface.cs
			private Thread _updateThread;
			private static CancellationTokenSource _cancellationToken;
			private static readonly VarjoTracker tracker = new VarjoTracker();

			public bool Initialize()
			{
				_cancellationToken?.Cancel();
				_updateThread?.Abort();
				return tracker.ConnectToPipe();
			}

			public void StartThread()
			{
				_cancellationToken = new CancellationTokenSource();
				_updateThread = new Thread(() =>
				{
					// IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get());
					while (!_cancellationToken.IsCancellationRequested)
					{
						Update();
						Thread.Sleep(10);
					}
				});
				_updateThread.Start();
			}

			public void Teardown()
			{
				_cancellationToken.Cancel();
				tracker.InterfaceTeardown();
				_cancellationToken.Dispose();
			}

			public void Update()
			{
				if (!Engine.Current.InputInterface.VR_Active)
					return;
				tracker.InterfaceUpdate();
			}
			// End VarjoTrackingInterface.cs
		}

		public class GenericInputDevice : IInputDriver
		{
			public Eyes eyes;
			public int UpdateOrder => 100;
			// These values need to be tweaked per user
			public float Alpha = 2f;
			public float Beta = 2f;
			// This is the user's average pupil size, in mm
			public float pupilSizeConstant = 4f;

			public void CollectDeviceInfos(DataTreeList list)
			{
				DataTreeDictionary EyeDataTreeDictionary = new DataTreeDictionary();
				EyeDataTreeDictionary.Add("Name", "Generic Eye Tracking");
				EyeDataTreeDictionary.Add("Type", "Eye Tracking");
				EyeDataTreeDictionary.Add("Model", "Generic Eye Model");
				list.Add(EyeDataTreeDictionary);
			}

			public void RegisterInputs(InputInterface inputInterface)
			{
				eyes = new Eyes(inputInterface, "Generic Eye Tracking");
			}

			public void UpdateInputs(float deltaTime)
			{
				// Current eye data assumes a data format similar to the Pimax
				eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;

				// Add Engine.Current.InputInterface.VR_Active to this?
				eyes.LeftEye.IsDeviceActive = varjoMemory.calibrated;
				eyes.RightEye.IsDeviceActive = varjoMemory.calibrated;
				eyes.CombinedEye.IsDeviceActive = varjoMemory.calibrated;
				eyes.Timestamp = DateTime.Now.Ticks;

				// Yes, it's a bool
				eyes.LeftEye.Openness = varjoMemory.leftEye.opened ? 1f : 0f;
				eyes.RightEye.Openness = varjoMemory.rightEye.opened ? 1f : 0f;
				eyes.CombinedEye.Openness = varjoMemory.combined.opened ? 1f : 0f;

				// Varjo returns a normalized float for pupilSize. Do we need to divide by a
				// maximum tracked pupil size constant?
				eyes.LeftEye.PupilDiameter = (float) varjoMemory.leftEye.pupilSize;
				eyes.RightEye.PupilDiameter = (float) varjoMemory.rightEye.pupilSize;
				eyes.CombinedEye.PupilDiameter = (float) varjoMemory.combined.pupilSize;

				// Funny Pimax Code
				eyes.LeftEye.Direction = new float3(MathX.Tan(Alpha * (float) varjoMemory.leftEye.x),
													  MathX.Tan(Beta * (-1f) * (float) varjoMemory.leftEye.y),
													  1f).Normalized;
				eyes.LeftEye.RawPosition = new float3((float) varjoMemory.leftEye.y * (-1f),
													  (float) varjoMemory.leftEye.x,
													  0f);

				eyes.RightEye.Direction = new float3(MathX.Tan(Alpha * (float) varjoMemory.rightEye.x),
									  MathX.Tan(Beta * (-1f) * (float) varjoMemory.rightEye.y),
									  1f).Normalized;
				eyes.RightEye.RawPosition = new float3((float)varjoMemory.rightEye.y * (-1f),
													  (float)varjoMemory.rightEye.x,
													  0f);

				eyes.CombinedEye.Direction = new float3(MathX.Average((float)MathX.Tan(Alpha * varjoMemory.leftEye.y), (float)MathX.Tan(Alpha * varjoMemory.rightEye.y)),
														 MathX.Average((float)MathX.Tan(Alpha * varjoMemory.leftEye.x), (float)MathX.Tan(Alpha * varjoMemory.rightEye.x)),
														 1f).Normalized;
				eyes.CombinedEye.RawPosition = new float3(MathX.Average((float)(varjoMemory.leftEye.x + varjoMemory.rightEye.x)),
														  MathX.Average((float)(varjoMemory.leftEye.y + varjoMemory.rightEye.x)),
														  0f);

				// Fake Widen and Squeeze based on a user's look up and down
				eyes.LeftEye.Widen = (float)MathX.Clamp01(varjoMemory.leftEye.y);
				eyes.LeftEye.Squeeze = (float)MathX.Remap(MathX.Clamp(varjoMemory.leftEye.y, -1f, 0f), -1f, 0f, 0f, 1f);

				eyes.RightEye.Widen = (float)MathX.Clamp01(varjoMemory.rightEye.y);
				eyes.RightEye.Squeeze = (float)MathX.Remap(MathX.Clamp(varjoMemory.rightEye.y, -1f, 0f), -1f, 0f, 0f, 1f);

				eyes.CombinedEye.Widen = MathX.Average((float)MathX.Clamp01(varjoMemory.leftEye.x), (float)MathX.Clamp01(varjoMemory.rightEye.y));
				eyes.CombinedEye.Squeeze = MathX.Average((float)MathX.Remap(MathX.Clamp(varjoMemory.leftEye.y, -1f, 0f), -1f, 0f, 0f, 1f),
                                                                (float)MathX.Remap(MathX.Clamp(varjoMemory.rightEye.y, -1f, 0f), -1f, 0f, 0f, 1f));

				// Not tracked
				eyes.LeftEye.Frown = 0f;
				eyes.RightEye.Frown = 0f;
				eyes.CombinedEye.Frown = 0f;
			}
		}
	}
}
