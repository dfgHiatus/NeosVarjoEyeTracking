using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using System.Collections.Generic;

namespace NeosVarjoEye
{
	public class NeosVarjoEye : NeosMod
	{
		// Ideally we could disable eye tracking on the fly, but once it's there we can't uninitialize it like the VPE
		// [AutoRegisterConfigKey]
		// public static ModConfigurationKey<bool> IS_ENABLED = new ModConfigurationKey<bool>("is_enabled", "Eye Tracking Enabled", () => true);

		// Hack to compensate for accurate timestamp. 
		// TODO: Update gazeData to have GazeOutputFrequency
		[AutoRegisterConfigKey]
		public static ModConfigurationKey<GazeOutputFrequency> DEVICE_FREQUENCY = new ModConfigurationKey<GazeOutputFrequency>("gaze_frequency", "Gaze Output Frequency (Used to compute timestamp)", () => GazeOutputFrequency.Frequency100Hz);
		public static readonly Dictionary<GazeOutputFrequency, int> freqDict = new Dictionary<GazeOutputFrequency, int>(){
			{ GazeOutputFrequency.MaximumSupported, 200 },
			{ GazeOutputFrequency.Frequency100Hz, 100 },
			{ GazeOutputFrequency.Frequency200Hz, 200 },
		};

		public static ModConfiguration config;
		public static GazeData gazeData;
		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.4";
		public override string Link => "https://github.com/dfgHiatus/NeosVarjoEyeTracking";
		public override void OnEngineInit()
		{
			// Harmony.DEBUG = true;
			Harmony harmony = new Harmony("net.dfgHiatus.NeosVarjoEyeTracking");
			config = GetConfiguration();
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
					if (VarjoTrackingModule.tracker.ConnectToPipe())
					{
						Debug("Gaze tracking enabled for Varjo HMD.");
						GenericInputDevice gen = new GenericInputDevice();
						__instance.RegisterInputDriver(gen);
					}
					else
						Warn("Varjo headset isn't detected.");
				}
				catch (Exception ex)
				{
					Warn("Module failed to initiallize.");
					Warn(ex.ToString());
				}
			}
		}

		[HarmonyPatch(typeof(Engine), "Shutdown")]
		public class ShutdownPatch
		{
			public static bool Prefix()
			{
				VarjoTrackingModule.tracker.Teardown();
				return true;
			}
		}

		public class GenericInputDevice : IInputDriver
		{
			public Eyes eyes;
			public int UpdateOrder => 100;

			// TODO: Update gazeData to have proper pupil dilation. I'll do this whern they add eye openess.
			// Idle pupil size in mm. Did you know the average human pupil size is between 4 and 6mm?
			// pupilSize is normalized from 0 to 1, so idle is 0.5 or ~3mm
			public float userPupilDiameter = 0.065f;

			public void CollectDeviceInfos(DataTreeList list)
			{
				DataTreeDictionary EyeDataTreeDictionary = new DataTreeDictionary();
				EyeDataTreeDictionary.Add("Name", "Varjo Eye Tracking");
				EyeDataTreeDictionary.Add("Type", "Eye Tracking");
				EyeDataTreeDictionary.Add("Model", "Varjo HMD");
				list.Add(EyeDataTreeDictionary);
			}

			public void RegisterInputs(InputInterface inputInterface)
			{
				eyes = new Eyes(inputInterface, "Varjo Eye Tracking");
			}

			public void UpdateInputs(float deltaTime)
			{
				// if (config.GetValue(IS_ENABLED))
                // {

					// Current eye data takes on a data format similar to the Pimax, x and y values normalized from -1 to 1
					eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;

					eyes.LeftEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
					eyes.LeftEye.IsTracking = gazeData.leftStatus == GazeEyeStatus.Compensated ||
						gazeData.leftStatus == GazeEyeStatus.Tracked;
					eyes.LeftEye.Direction = (float3)new double3(gazeData.leftEye.forward.x,
														  gazeData.leftEye.forward.y,
														  gazeData.leftEye.forward.z);
					eyes.LeftEye.RawPosition = (float3)new double3(gazeData.leftEye.origin.x,
														  gazeData.leftEye.origin.y,
														  gazeData.leftEye.origin.z);
					if (gazeData.leftStatus != GazeEyeStatus.Invalid)
					{
						eyes.LeftEye.PupilDiameter = (float)(gazeData.leftPupilSize * userPupilDiameter);
					}
					eyes.LeftEye.Openness = gazeData.leftStatus == GazeEyeStatus.Invalid ? 0f : 1f; 
					eyes.LeftEye.Widen = (float)MathX.Clamp01(gazeData.leftEye.forward.y);
					eyes.LeftEye.Squeeze = 0f;
					eyes.LeftEye.Frown = 0f;

					eyes.RightEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
					eyes.RightEye.IsTracking = gazeData.rightStatus == GazeEyeStatus.Compensated ||
						gazeData.rightStatus == GazeEyeStatus.Tracked;
					eyes.RightEye.Direction = (float3)new double3(gazeData.rightEye.forward.x,
														  gazeData.rightEye.forward.y,
														  gazeData.rightEye.forward.z);
					eyes.RightEye.RawPosition = (float3)new double3(gazeData.rightEye.origin.x,
														  gazeData.rightEye.origin.y,
														  gazeData.rightEye.origin.z);
					if (gazeData.rightStatus != GazeEyeStatus.Invalid)
					{
						eyes.RightEye.PupilDiameter = (float)(gazeData.rightPupilSize * userPupilDiameter);
					}
					eyes.RightEye.Openness = gazeData.rightStatus == GazeEyeStatus.Invalid ? 0f : 1f;
					eyes.RightEye.Widen = (float)MathX.Clamp01(gazeData.rightEye.forward.y);
					eyes.RightEye.Squeeze = 0f;
					eyes.RightEye.Frown = 0f;

					eyes.CombinedEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
					eyes.CombinedEye.IsTracking = gazeData.status == GazeStatus.Valid;
					eyes.CombinedEye.Direction = (float3)new double3(gazeData.gaze.forward.x,
														  gazeData.gaze.forward.y,
														  gazeData.gaze.forward.z);
					eyes.CombinedEye.RawPosition = (float3)new double3(gazeData.gaze.origin.x,
														  gazeData.gaze.origin.y,
														  gazeData.gaze.origin.z);
					if (gazeData.leftStatus != GazeEyeStatus.Invalid &&
						gazeData.rightStatus != GazeEyeStatus.Invalid)
					{
						eyes.CombinedEye.PupilDiameter = MathX.Average((float)(gazeData.leftPupilSize * userPupilDiameter),
											(float)(gazeData.rightPupilSize * userPupilDiameter));
					}
					eyes.CombinedEye.Openness = gazeData.leftStatus == GazeEyeStatus.Invalid ||
						gazeData.rightStatus == GazeEyeStatus.Invalid ? 0f : 1f;
					eyes.CombinedEye.Widen = (float)MathX.Clamp01(gazeData.gaze.forward.y);
					eyes.CombinedEye.Squeeze = 0f;
					eyes.CombinedEye.Frown = 0f;

					// Convergence Distance is NOT Focus Distance, but we need to get it in somehow
					if (gazeData.stability > 0.75) {
						eyes.ConvergenceDistance = (float) gazeData.focusDistance;
					}

				/*
				* Copied from https://developer.varjo.com/docs/native/introduction-to-varjo-sdk:
				* 
				* Timing
					"Varjo uses nanoseconds as a time unit ...
					Because measuring time in nanoseconds yields very large numbers, you should be aware of possible precision issues when casting to other types."
				*
				* Yeah, gazeData.captureTime is wonky. It straight up does *NOT* want to work with Neos. 
				* Oh well, gazeData.frameNumber it is! gazeData.frameNumber / GazeOutputFrequency will be correct anyways
				*/

				eyes.Timestamp = gazeData.frameNumber / freqDict[config.GetValue(DEVICE_FREQUENCY)];
                // }	
			}
		}
	}
}
