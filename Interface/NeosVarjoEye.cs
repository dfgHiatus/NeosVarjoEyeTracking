using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using VarjoInterface;
using VarjoInterface.Native;
using VarjoInterface.Companion;

namespace NeosVarjoEye
{
	public class NeosVarjoEye : NeosMod
	{
		[AutoRegisterConfigKey]
		public static ModConfigurationKey<bool> usingCompanion = new ModConfigurationKey<bool>("using_companion", "Use the Varjo Companion app (requires restart)", () => true);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<bool> blinkDetection = new ModConfigurationKey<bool>("using_companion", "Use Blink Smoothing", () => true);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> userPupilScale = new ModConfigurationKey<float>("blink_Speed", "Pupil Dilation Scale. Used to correct Varjo Companion readings", () => 0.001f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> blinkSpeed = new ModConfigurationKey<float>("blink_Speed", "Blink Speed", () => 10.0f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> middleStateSpeedMultiplier = new ModConfigurationKey<float>("middle_State_Speed_Multiplier", "Middle State Speed Multiplier", () => 0.025f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> blinkDetectionMultiplier = new ModConfigurationKey<float>("blink_Detection_Multiplier", "Blink Detection Multiplier", () => 2.0f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> fullOpenState = new ModConfigurationKey<float>("full_Open_State", "Fully Open State", () => 1.0f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> halfOpenState = new ModConfigurationKey<float>("half_Open_State", "Half Open State", () => 0.5f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> quarterOpenState = new ModConfigurationKey<float>("quarter_Open_State", "Quarter Open State", () => 0.25f);

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<float> closedState = new ModConfigurationKey<float>("closed_State", "Eye Closed State", () => 0.0f);

		public static ModConfiguration config;

		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.6";
		public override string Link => "https://github.com/dfgHiatus/NeosVarjoEyeTracking";
		public override void OnEngineInit()
		{
			config = GetConfiguration();
			new Harmony("net.dfgHiatus.NeosVarjoEyeTracking").PatchAll();
		}

		private static VarjoModule tracker;
		private static bool isCompanion;

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new Type[] { typeof(Engine) })]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					if (config.GetValue(usingCompanion))
					{
						tracker = new VarjoCompanionInterface();
					}
					else
					{
						tracker = new VarjoNativeInterface();
					}
					Debug(string.Format("Initializing {0} Varjo module", tracker.GetName()));
					bool pipeConnected = tracker.Initialize();
					isCompanion = config.GetValue(usingCompanion);
					GenericInputDevice genericInputDevice = new GenericInputDevice();
					__instance.RegisterInputDriver(genericInputDevice);
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
				tracker.Teardown();
				return true;
			}
		}

		public class GenericInputDevice : IInputDriver
		{	
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
				tracker.Update();
				var gazeData = tracker.GetGazeData();
				var eyeData = tracker.GetEyeMeasurements();

				var leftPupil = isCompanion ?
					(float)(gazeData.leftPupilSize * config.GetValue(userPupilScale)) :
					eyeData.rightPupilDiameterInMM;
				var rightPupil = isCompanion ?
					(float)(gazeData.leftPupilSize * config.GetValue(userPupilScale)) :
					eyeData.leftPupilDiameterInMM;

				var leftOpen =
					gazeData.leftStatus == GazeEyeStatus.Tracked ? config.GetValue(fullOpenState) : (
					gazeData.leftStatus == GazeEyeStatus.Compensated ? config.GetValue(halfOpenState) : (
					gazeData.leftStatus == GazeEyeStatus.Visible ? config.GetValue(quarterOpenState)
					: config.GetValue(closedState))); // GazeEyeStatus.Invalid

				var rightOpen =
					gazeData.rightStatus == GazeEyeStatus.Tracked ? config.GetValue(fullOpenState) : (
					gazeData.rightStatus == GazeEyeStatus.Compensated ? config.GetValue(halfOpenState) : (
					gazeData.rightStatus == GazeEyeStatus.Visible ? config.GetValue(quarterOpenState)
					: config.GetValue(closedState))); // GazeEyeStatus.Invalid
				
				if (config.GetValue(blinkDetection))
				{
					if (_previouslyClosedLeft == true && gazeData.leftStatus == GazeEyeStatus.Invalid)
					{
						_leftEyeBlinkMultiplier += config.GetValue(blinkDetectionMultiplier);
					}
					else if (gazeData.leftStatus == GazeEyeStatus.Compensated || gazeData.leftStatus == GazeEyeStatus.Visible)
					{
						_leftEyeBlinkMultiplier *= config.GetValue(middleStateSpeedMultiplier);
						_leftEyeBlinkMultiplier = MathX.Max(1.0f, _leftEyeBlinkMultiplier);
					}
					else
					{
						_leftEyeBlinkMultiplier = 1.0f;
					}

					if (_previouslyClosedRight == true && gazeData.rightStatus == GazeEyeStatus.Invalid)
					{
						_rightEyeBlinkMultiplier += config.GetValue(blinkDetectionMultiplier);
					}
					else if (gazeData.rightStatus == GazeEyeStatus.Compensated || gazeData.rightStatus == GazeEyeStatus.Visible)
					{
						_rightEyeBlinkMultiplier *= config.GetValue(middleStateSpeedMultiplier);
						_rightEyeBlinkMultiplier = MathX.Max(1.0f, _rightEyeBlinkMultiplier);
					}
					else
					{
						_rightEyeBlinkMultiplier = 1.0f;
					}

					_previouslyClosedLeft = gazeData.leftStatus == GazeEyeStatus.Invalid;
					_previouslyClosedRight = gazeData.rightStatus == GazeEyeStatus.Invalid;
				}

				_leftOpen = MathX.Lerp(_leftOpen, leftOpen, deltaTime * config.GetValue(blinkSpeed) * _leftEyeBlinkMultiplier);

				_rightOpen = MathX.Lerp(_rightOpen, rightOpen, deltaTime * config.GetValue(blinkSpeed) * _rightEyeBlinkMultiplier);

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

				_combinedOpen = MathX.Lerp(_combinedOpen, combinedOpen, deltaTime * config.GetValue(blinkSpeed));
				
				UpdateEye(in gazeData.gaze, in combinedStatus, in combinedPupil, _combinedOpen, deltaTime, eyes.CombinedEye);

				eyes.ComputeCombinedEyeParameters();

				// Yes, I am aware convergence distance and focus distance are NOT the same thing.
				if (gazeData.stability > 0.75) {
					eyes.ConvergenceDistance = (float) gazeData.focusDistance;
				}

				/*
				* Yeah, gazeData.captureTime is wonky. gazeData.frameNumber it is! 
				* You don't need 100hz+ sampling in a networked social context anyways, right?
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
