using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using Newtonsoft.Json;
using System.IO;

namespace NeosVarjoEye
{
	public class NeosVarjoEye : NeosMod
	{
		public static GazeData gazeData;
		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.4";
		public override string Link => "https://github.com/dfgHiatus/NeosVarjoEyeTracking";

		public override void OnEngineInit() => new Harmony("net.dfgHiatus.NeosVarjoEyeTracking").PatchAll();

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new System.Type[] { typeof(Engine) })]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					if (VarjoTrackingModule.tracker.ConnectToPipe())
					{
						Debug("Gaze tracking enabled for Varjo HMD.");
						GenericInputDevice genericInputDevice = new GenericInputDevice();
						__instance.RegisterInputDriver(genericInputDevice);
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
			[Serializable]
			public class Settings
			{
				public float userPupilScale = 0.001f;
				public float blinkSpeed = 10.0f;
				public bool blinkDetection = true;
				public float middleStateSpeedMultiplier = 0.025f;
				public float blinkDetectionMultiplier = 2.0f;

				public float fullOpenState = 1.0f;
				public float halfOpenState = 0.5f;
				public float quarterOpenState = 0.25f;
				public float closedState = 0.0f;
			}

			public Settings eyeTrackingSettings = new Settings();
			
			public Eyes eyes;
			
			public int UpdateOrder => 100;

			private float _leftOpen = 1.0f;
			private float _rightOpen = 1.0f;
			private float _combinedOpen = 1.0f;
			
			private bool _previouslyClosedRight = false;
			private bool _previouslyClosedLeft = false;

			private float _leftEyeBlinkMultiplier = 1.0f;
			private float _rightEyeBlinkMultiplier = 1.0f;

			private const string EYETRACKING_CONFIG_FILE = "nml_config/varjo.neos.eyetracking.json";

			public GenericInputDevice()
			{
				// Check if our config file is present.

				if (File.Exists(EYETRACKING_CONFIG_FILE))
				{
					eyeTrackingSettings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(EYETRACKING_CONFIG_FILE));
				}
				else
				{
					// Create the file, populated it with some defaults.
					File.WriteAllText(EYETRACKING_CONFIG_FILE, JsonConvert.SerializeObject(eyeTrackingSettings));
				}
			}

			public void CollectDeviceInfos(DataTreeList list)
			{
				DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
				dataTreeDictionary.Add("Name", "Varjo Eye Tracking");
				dataTreeDictionary.Add("Type", "Eye Tracking");
				dataTreeDictionary.Add("Model", "Varjo HMD");
				list.Add(dataTreeDictionary);
			}

			public void RegisterInputs(InputInterface inputInterface)
			{
				eyes = new Eyes(inputInterface, "Varjo Eye Tracking");
			}

			public void UpdateInputs(float deltaTime)
			{
				// Updating this on the main thread isn't the optimal idea.
				// But giving it its own thread messes around with things for some reason. So here it lies
				VarjoTrackingModule.tracker.Update();

				var leftPupil = (float)(gazeData.leftPupilSize * eyeTrackingSettings.userPupilScale);
				var rightPupil = (float)(gazeData.leftPupilSize * eyeTrackingSettings.userPupilScale);

				var leftOpen =
					gazeData.leftStatus == GazeEyeStatus.Tracked ? eyeTrackingSettings.fullOpenState : (
					gazeData.leftStatus == GazeEyeStatus.Compensated ? eyeTrackingSettings.halfOpenState : (
					gazeData.leftStatus == GazeEyeStatus.Visible ? eyeTrackingSettings.quarterOpenState
					: eyeTrackingSettings.closedState)); // GazeEyeStatus.Invalid

				var rightOpen =
					gazeData.rightStatus == GazeEyeStatus.Tracked ? eyeTrackingSettings.fullOpenState : (
					gazeData.rightStatus == GazeEyeStatus.Compensated ? eyeTrackingSettings.halfOpenState : (
					gazeData.rightStatus == GazeEyeStatus.Visible ? eyeTrackingSettings.quarterOpenState
					: eyeTrackingSettings.closedState)); // GazeEyeStatus.Invalid
				
				if (eyeTrackingSettings.blinkDetection)
				{
					if (_previouslyClosedLeft == true && gazeData.leftStatus == GazeEyeStatus.Invalid)
					{
						_leftEyeBlinkMultiplier += eyeTrackingSettings.blinkDetectionMultiplier;
					}
					else if (gazeData.leftStatus == GazeEyeStatus.Compensated || gazeData.leftStatus == GazeEyeStatus.Visible)
					{
						_leftEyeBlinkMultiplier *= eyeTrackingSettings.middleStateSpeedMultiplier;
						_leftEyeBlinkMultiplier = MathX.Max(1.0f, _leftEyeBlinkMultiplier);
					}
					else
					{
						_leftEyeBlinkMultiplier = 1.0f;
					}

					if (_previouslyClosedRight == true && gazeData.rightStatus == GazeEyeStatus.Invalid)
					{
						_rightEyeBlinkMultiplier += eyeTrackingSettings.blinkDetectionMultiplier;
					}
					else if (gazeData.rightStatus == GazeEyeStatus.Compensated || gazeData.rightStatus == GazeEyeStatus.Visible)
					{
						_rightEyeBlinkMultiplier *= eyeTrackingSettings.middleStateSpeedMultiplier;
						_rightEyeBlinkMultiplier = MathX.Max(1.0f, _rightEyeBlinkMultiplier);
					}
					else
					{
						_rightEyeBlinkMultiplier = 1.0f;
					}

					_previouslyClosedLeft = gazeData.leftStatus == GazeEyeStatus.Invalid;
					_previouslyClosedRight = gazeData.rightStatus == GazeEyeStatus.Invalid;
				}
				
				_leftOpen = MathX.Lerp(_leftOpen, leftOpen, deltaTime * eyeTrackingSettings.blinkSpeed * _leftEyeBlinkMultiplier);

				_rightOpen = MathX.Lerp(_rightOpen, rightOpen, deltaTime * eyeTrackingSettings.blinkSpeed * _rightEyeBlinkMultiplier);

				bool leftStatus = gazeData.leftStatus == GazeEyeStatus.Compensated ||
				                  gazeData.leftStatus == GazeEyeStatus.Tracked ||
				                  gazeData.leftStatus == GazeEyeStatus.Visible;
				
				bool rightStatus = gazeData.rightStatus == GazeEyeStatus.Compensated ||
				                   gazeData.rightStatus == GazeEyeStatus.Tracked ||
				                   gazeData.rightStatus == GazeEyeStatus.Visible;

				eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;
				
				UpdateEye(in gazeData.leftEye, in leftStatus, in leftPupil, _leftOpen, deltaTime, eyes.LeftEye);
				UpdateEye(in gazeData.rightEye, in rightStatus, in rightPupil, _rightOpen, deltaTime, eyes.RightEye);
				
				bool combinedStatus = gazeData.status == GazeStatus.Valid;
				float combinedPupil = MathX.Average(leftPupil, rightPupil);
				float combinedOpen = MathX.Average(leftOpen, rightOpen);

				_combinedOpen = MathX.Lerp(_combinedOpen, combinedOpen, deltaTime * eyeTrackingSettings.blinkSpeed);
				
				UpdateEye(in gazeData.gaze, in combinedStatus, in combinedPupil, _combinedOpen, deltaTime, eyes.CombinedEye);

				eyes.ComputeCombinedEyeParameters();

				// Yes, I am aware convergence distance and focus distance are NOT the same thing.
				// But we ought to get it in somehow.
				if (gazeData.stability > 0.75) {
					eyes.ConvergenceDistance = (float) gazeData.focusDistance;
				}

				/*
				* Yeah, gazeData.captureTime is wonky. gazeData.frameNumber it is! 
				* We need a hardcoded value to divide it by bc mod config go brr
				* I mean, you don't need 100hz+ sampling in a networked social context anyways, right? Right!
				*/

				eyes.Timestamp = gazeData.frameNumber / 100;
				
				eyes.FinishUpdate();
			}

			void UpdateEye(in GazeRay data, in bool status, in float pupilSize, in float openness, in float deltaTime, Eye eye)
			{
				eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eye.IsTracking = status;

				if (eye.IsTracking)
				{
					eye.UpdateWithDirection((float3)new double3(data.forward.x,
						data.forward.y,
						data.forward.z).Normalized);
					
					eye.RawPosition = (float3)new double3(data.origin.x,
						data.origin.y,
						data.origin.z);
					
					eye.PupilDiameter = pupilSize;
				}

				eye.Openness = openness;
				eye.Widen = (float)MathX.Clamp01(data.forward.y);
				eye.Squeeze = 0f;
				eye.Frown = 0f;
			}
		}
	}
}
