using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;
using KSP.IO;

using UnityEngine;
using UnityEngine.UI;

namespace FatHand
{
	[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
	public class ManeuverQueue : MonoBehaviour
	{

		protected const double minimumManeuverDeltaT = 15.0 * 60.0;
		const float WINDOW_VERTICAL_POSITION = 36;

		public static string[] FilterModeLabels = new string[] {
			"MET", "MNV", "A-Z" };
		public enum FilterMode
		{
			Undefined = -1,
			Default,
			Maneuver,
			Name
		};

		protected SpaceTracking spaceTrackingScene;
		protected Rect windowPos;
		protected GUIStyle windowStyle;
		protected bool delaySetMode;
		protected bool render;
		protected bool needsRerender;
		protected bool needsWidgetColorRender;
		protected Color nodePassedColor = new Color(255.0f / 255, 58.0f / 255, 58.0f / 255, 1);
		protected Color nodeWarningColor = new Color(255.0f / 255, 255.0f / 255, 58.0f / 255, 1);

		private FilterMode _currentMode = FilterMode.Undefined;
		public FilterMode CurrentMode
		{
			get => _currentMode;
			set
			{
				// rebuild the list if the value is changed or the list is uninitialized
				if (value != _currentMode || _defaultVessels == null)
				{
					// if we're switching from any mode other than maneuver mode, save the filter state
					if (_currentMode != FilterMode.Maneuver && _currentMode != FilterMode.Undefined)
						savedFilterState = MapViewFiltering.vesselTypeFilter;

					_currentMode = value;
					SetVesselListForMode(_currentMode);

					// Unless the mode is undefined, persist the value (saved in onDestroy)
					if (_currentMode != FilterMode.Undefined)
						pluginConfiguration.SetValue(configurationModeKey, (int)_currentMode);
				}
			}
		}

		private List<Vessel> _defaultVessels;
		protected List<Vessel> DefaultVessels
		{
			get
			{
				if (_defaultVessels == null && spaceTrackingScene != null && trackedVesselsField != null)
				{
					Log("Getting list of tracked vessels");
					var trackedVessels =
						trackedVesselsField.GetValue(spaceTrackingScene) as List<Vessel>;
					if (trackedVessels != null)
						_defaultVessels = new List<Vessel>(trackedVessels);
					/*else
					// it will get the list eventually (from Update())
					{
						Log("Tracked vessels not initialized yet, building our own");
						var vessels = FlightGlobals.Vessels;
						_defaultVessels = new List<Vessel>(vessels.Count);
						foreach (var vessel in vessels)
							if (vessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.StateVectors))
								_defaultVessels.Add(vessel);
						Log("Got {0} vessels out of {1} total", _defaultVessels.Count, vessels.Count);
					}*/
				}
				return _defaultVessels;
			}
			set
			{
				_defaultVessels = value;
				_vesselsSortedByName = null;
				_vesselsSortedByNextManeuverNode = null;
				_guardedVessels = null;
			}
		}

		private List<Vessel> _vesselsSortedByNextManeuverNode;
		protected List<Vessel> VesselsSortedByNextManeuverNode
		{
			get
			{
				if (_vesselsSortedByNextManeuverNode == null && DefaultVessels != null)
				{
					_vesselsSortedByNextManeuverNode = _defaultVessels.Where(vessel =>
						(NextManeuverNodeForVessel(vessel) != null)).ToList();
					_vesselsSortedByNextManeuverNode.Sort((x, y) =>
						NextManeuverNodeForVessel(x)
						.UT.CompareTo(NextManeuverNodeForVessel(y).UT));
				}
				return _vesselsSortedByNextManeuverNode;
			}
			set => _vesselsSortedByNextManeuverNode = value;
		}

		// vessels with maneuver nodes in the future
		// 'guarded' - prevent warping beyond next node
		private List<Vessel> _guardedVessels;
		protected List<Vessel> GuardedVessels
		{
			get => _guardedVessels ?? (_guardedVessels =
				VesselsSortedByNextManeuverNode?.Where(vessel =>
					NextManeuverNodeForVessel(vessel).UT
					- Planetarium.GetUniversalTime()
					> minimumManeuverDeltaT)
				.ToList());
			set => _guardedVessels = value;
		}

		private List<Vessel> _vesselsSortedByName;
		protected List<Vessel> vesselsSortedByName
		{
			get
			{
				if (_vesselsSortedByName == null && DefaultVessels != null)
				{
					_vesselsSortedByName = new List<Vessel>(_defaultVessels);
					_vesselsSortedByName.Sort((x, y) =>
						x.vesselName.CompareTo(y.vesselName));
				}
				return _vesselsSortedByName;
			}
			set => _vesselsSortedByName = value;
		}

		private static string configurationModeKey = "mode";
		private static string configurationFiltersKey = "filters";
		private PluginConfiguration pluginConfiguration = PluginConfiguration.CreateForType<ManeuverQueue>();
		private Rect sideBarRect;
		private static MapViewFiltering.VesselTypeFilter savedFilterState;

		// Lifecycle
		protected void Awake()
		{
		}

		protected void Start()
		{
			Log("Start");

			spaceTrackingScene = FindObjectOfType<SpaceTracking>();
			//works for KSP_64.exe using KSP_x64_Data\Managed\Assembly-CSharp.dll
			trackedVesselsField = GetSpaceTrackingField("trackedVessels");
			vesselWidgetsField = GetSpaceTrackingField("vesselWidgets");
			vesselImageField = GetVesselIconSpriteField("image");
			if (trackedVesselsField == null || vesselWidgetsField == null)
			{
				//works for KSP.exe using obfuscated KSP_Data\Managed\Assembly-CSharp.dll
				trackedVesselsField = GetSpaceTrackingField("\x3");
				vesselWidgetsField = GetSpaceTrackingField("\x1");
				vesselImageField = GetVesselIconSpriteField("\x1");
				if (trackedVesselsField != null
					&& vesselWidgetsField != null
					&& vesselImageField != null
					&& trackedVesselsField.FieldType == typeof(List<Vessel>)
					&& vesselWidgetsField.FieldType == typeof(List<TrackingStationWidget>)
					&& vesselImageField.FieldType == typeof(Image))
				{
					Debug.Log("ManeuverQueue: obfuscated Assembly-CSharp.dll detected");
				}
				else
				{//	should work in any version (unless there are more fields with same type as those two we need)
					trackedVesselsField = null;
					vesselWidgetsField = null;
					vesselImageField = null;
					System.Text.StringBuilder sb = null;
					foreach (var f in typeof(SpaceTracking).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						bool valid = true;
						foreach (char c in f.Name)
						{
							if (c != '_' && !char.IsLetterOrDigit(c))
							{
								valid = false;
								break;
							}
						}
						if (valid) Debug.Log(string.Format(
							"ManeuverQueue: SpaceTracking.{0}: {1}",
							f.Name, f.FieldType.FullName));
						else
						{
							if (sb == null) sb = new System.Text.StringBuilder();
							sb.Length = 0;
							sb.Append("ManeuverQueue: SpaceTracking.");
							foreach (char c in f.Name)
							{
								if (c != '_' && !char.IsLetterOrDigit(c))
									sb.AppendFormat("\\x{0:X}", (uint)c);
								else sb.Append(c);
							}
							sb.AppendFormat("({0}): {1}", f.Name.Length, f.FieldType.FullName);
							Debug.Log(sb.ToString());
							if ((trackedVesselsField == null || f.Name == "\x3")
								&& f.FieldType == typeof(List<Vessel>))
								trackedVesselsField = f;
							if ((vesselWidgetsField == null || f.Name == "\x1")
								&& f.FieldType == typeof(List<TrackingStationWidget>))
								vesselWidgetsField = f;
						}
					}
					if (trackedVesselsField == null || vesselWidgetsField == null)
					{
						Debug.Log("ManeuverQueue: Could not get trackedVessels/vesselWidgets FieldInfo, plugin will be disabled");
						return;
					}
					//note: it is not critical if we cannot change the color
					//  =>  no check for vesselImageField above, but we will try here
					foreach (var f in typeof(VesselIconSprite).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						bool valid = true;
						foreach (char c in f.Name)
						{
							if (c != '_' && !char.IsLetterOrDigit(c))
							{
								valid = false;
								break;
							}
						}
						if (valid) Debug.Log(string.Format(
							"ManeuverQueue: VesselIconSprite.{0}: {1}",
							f.Name, f.FieldType.FullName));
						else
						{
							if (sb == null) sb = new System.Text.StringBuilder();
							sb.Length = 0;
							sb.Append("ManeuverQueue: VesselIconSprite.");
							foreach (char c in f.Name)
							{
								if (c != '_' && !char.IsLetterOrDigit(c))
									sb.AppendFormat("\\x{0:X}", (uint)c);
								else sb.Append(c);
							}
							sb.AppendFormat("({0}): {1}", f.Name.Length, f.FieldType.FullName);
							Debug.Log(sb.ToString());
							if ((vesselImageField == null || f.Name == "\x1")
								&& f.FieldType == typeof(Image))
								vesselImageField = f;
						}
					}
				}
			}

			pluginConfiguration.load();

			savedFilterState =
				MapViewFiltering.vesselTypeFilter == MapViewFiltering.VesselTypeFilter.All
				? (MapViewFiltering.VesselTypeFilter)pluginConfiguration
					.GetValue(configurationFiltersKey,
					(int)MapViewFiltering.VesselTypeFilter.All)
				: MapViewFiltering.vesselTypeFilter;

			pluginConfiguration.SetValue(configurationFiltersKey,
				(int)MapViewFiltering.VesselTypeFilter.All);
			pluginConfiguration.save();

			sideBarRect = GetSideBarRect();

			windowPos = new Rect(sideBarRect.xMax, WINDOW_VERTICAL_POSITION, 10, 10);

			windowStyle = new GUIStyle(HighLogic.Skin.window)
			{
				margin = new RectOffset(),
				padding = new RectOffset(5, 5, 5, 5)
			};

			GameEvents.onGameSceneSwitchRequested.Add(onGameSceneSwitchRequested);

			GameEvents.onVesselDestroy.Add(onVesselDestroy);
			GameEvents.onVesselCreate.Add(onVesselCreate);
			GameEvents.onKnowledgeChanged.Add(onKnowledgeChanged);
			GameEvents.OnMapViewFiltersModified.Add(onMapViewFiltersModified);

			if (DefaultVessels != null)
			{
				delaySetMode = false;
				CurrentMode = (FilterMode)pluginConfiguration.GetValue(configurationModeKey, (int)FilterMode.Default);
			}
			else
			{
				delaySetMode = true;
				Log("Could not get vessels in Start(), delaying");
			}

			render = true;

			Log("Start Finished");
		}

		protected void Update()
		{
			if (trackedVesselsField == null)
				return; //won't work

			if(delaySetMode)
			{
				Log("Delayed set mode");
				delaySetMode = false;
				CurrentMode = (FilterMode)pluginConfiguration.GetValue(configurationModeKey, (int)FilterMode.Default);
			}
			if (GuardedVessels?.Count > 0 &&
				NextManeuverNodeForVessel(GuardedVessels[0]).UT
				- Planetarium.GetUniversalTime()
				<= minimumManeuverDeltaT)
			{
				TimeWarp.SetRate(0, true, true);
				GuardedVessels = null;
			}
		}

		protected void FixedUpdate()
		{
		}

		private void onMapEntered()
		{
			pluginConfiguration.load();

			MapViewFiltering.VesselTypeFilter stateToRestore =
				(MapViewFiltering.VesselTypeFilter)pluginConfiguration
				.GetValue(configurationFiltersKey,
				(int)MapViewFiltering.VesselTypeFilter.All);
			if (stateToRestore != MapViewFiltering.VesselTypeFilter.All)
			{
				MapViewFiltering.SetFilter(stateToRestore);
				pluginConfiguration.SetValue(configurationFiltersKey, savedFilterState);
				pluginConfiguration.save();
			}

			GameEvents.OnMapEntered.Remove(onMapEntered);
		}

		private void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> data)
		{
			render = false;
		}

		protected void OnDestroy()
		{
			GameEvents.onGameSceneSwitchRequested.Remove(onGameSceneSwitchRequested);

			GameEvents.onVesselDestroy.Remove(onVesselDestroy);
			GameEvents.onVesselCreate.Remove(onVesselCreate);
			GameEvents.onKnowledgeChanged.Remove(onKnowledgeChanged);
			GameEvents.OnMapViewFiltersModified.Remove(onMapViewFiltersModified);

			// This is a hack to ensure filter settings are retained
			// Necessary because there doesn't seem to be any reliable way to restore filters when leaving the tracking station
			pluginConfiguration.SetValue(configurationFiltersKey, (int)savedFilterState);
			GameEvents.OnMapEntered.Add(onMapEntered);

			pluginConfiguration.save();

		}

		protected void OnGUI()
		{
			if (render)
			{
				windowPos = GUILayout.Window(1, windowPos, ToolbarWindow, "", windowStyle, new GUILayoutOption[0]);

				if (needsRerender)
				{
					SetVesselListForMode(CurrentMode);
					needsRerender = false;
				}

				if (CurrentMode == FilterMode.Maneuver)
				{

					// apply shading to vessel icons for maneuver nodes that have just moved into the past or soon state
					foreach (TrackingStationWidget widget in GetTrackingStationWidgets())
						UpdateWidgetColorForCurrentTime(widget);

					// reapply shading for close maneuver nodes if necessary
					if (needsWidgetColorRender)
						RenderWidgetColors();
				}
			}
		}

		protected void SetVesselListForMode(FilterMode mode)
		{
			switch (mode)
			{
				case FilterMode.Undefined:
					break;
				case FilterMode.Default:
					SetVesselList(DefaultVessels, savedFilterState);
					break;
				case FilterMode.Maneuver:
					SetVesselList(VesselsSortedByNextManeuverNode, MapViewFiltering.VesselTypeFilter.All);
					break;
				case FilterMode.Name:
					SetVesselList(vesselsSortedByName, savedFilterState);
					break;
				default:
					SetVesselList(DefaultVessels, savedFilterState);
					break;
			}
		}

		protected void SetVesselList(List<Vessel> vessels, MapViewFiltering.VesselTypeFilter filters)
		{
			if (spaceTrackingScene == null || vessels == null || trackedVesselsField == null)
				return;
			trackedVesselsField.SetValue(spaceTrackingScene, vessels);

			MethodInfo clearMethod = typeof(SpaceTracking)
				.GetMethod("ClearUIList", BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo constructMethod = typeof(SpaceTracking)
				.GetMethod("ConstructUIList", BindingFlags.NonPublic | BindingFlags.Instance);

			clearMethod.Invoke(spaceTrackingScene, new object[0]);
			constructMethod.Invoke(spaceTrackingScene, new object[0]);

			MapViewFiltering.SetFilter(filters);

			ResetWidgetsForActiveVessel();
		}

		protected void RenderWidgetColors()
		{
			needsWidgetColorRender = false;

			// apply shading to vessel icons
			if (CurrentMode == FilterMode.Maneuver)
			{
				for (var i = 0; i < VesselsSortedByNextManeuverNode.Count - 1; ++i)
				{
					Vessel vessel = VesselsSortedByNextManeuverNode[i];
					Vessel nextVessel = VesselsSortedByNextManeuverNode[i + 1];

					TrackingStationWidget vesselWidget = GetWidgetForVessel(vessel);

					double mnvTime1 = NextManeuverNodeForVessel(vessel).UT;
					double mnvTime2 = NextManeuverNodeForVessel(nextVessel).UT;

					// if two maneuver nodes are less than minimumManeuverDeltaT secs apart - yellow
					if (mnvTime2 - mnvTime1 < minimumManeuverDeltaT)
					{
						var nextVesselWidget = GetWidgetForVessel(nextVessel);
						if (vesselWidget != null)
							ApplyColorToVesselWidget(vesselWidget, nodeWarningColor);
						if (nextVesselWidget != null)
							ApplyColorToVesselWidget(nextVesselWidget, nodeWarningColor);
					}
					if (vesselWidget)
						UpdateWidgetColorForCurrentTime(vesselWidget);
				}
			}
		}

		protected string StatusStringForVessel(Vessel vessel)
		{
			ManeuverNode node = NextManeuverNodeForVessel(vessel);
			return node == null ? "None" :
				"dV - " + Convert.ToInt16(node.DeltaV.magnitude) + "m/s";
		}

		protected ManeuverNode NextManeuverNodeForVessel(Vessel vessel)
		{
			if (vessel.flightPlanNode != null && vessel.flightPlanNode.HasNode("MANEUVER"))
			{
				ManeuverNode node = new ManeuverNode();
				node.Load(vessel.flightPlanNode.GetNode("MANEUVER"));
				return node;
			}

			return null;
		}

		protected void ApplyColorToVesselWidget(TrackingStationWidget widget, Color color)
		{
			if (vesselImageField == null) return; //it is not critical if we cannot change the color
			var image = (Image)vesselImageField.GetValue(widget.iconSprite);
			image.color = color;
		}

		protected void UpdateWidgetColorForCurrentTime(TrackingStationWidget widget)
		{
			ManeuverNode node = NextManeuverNodeForVessel(widget.vessel);
			if (node == null)
				return;

			double maneuverTime = node.UT;

			// if the maneuver node is less than 15mins away - yellow
			if (maneuverTime < Planetarium.GetUniversalTime() + minimumManeuverDeltaT)
				ApplyColorToVesselWidget(widget, nodeWarningColor);

			// if the maneuver nodes is in the past - red
			if (maneuverTime < Planetarium.GetUniversalTime())
				ApplyColorToVesselWidget(widget, nodePassedColor);
		}

		protected List<TrackingStationWidget> GetTrackingStationWidgets() =>
			vesselWidgetsField.GetValue(spaceTrackingScene) as List<TrackingStationWidget>;

		protected TrackingStationWidget GetWidgetForVessel(Vessel vessel)
		{
			foreach (TrackingStationWidget widget in GetTrackingStationWidgets())
			{
				if (widget.vessel == vessel)
					return widget;
			}
			return null;
		}

		protected Vessel SelectedVessel
		{
			get => spaceTrackingScene.SelectedVessel;
			set => spaceTrackingScene.SelectedVessel = value;
		}

		protected void ResetWidgetsForActiveVessel()
		{
			Vessel selectedVessel = SelectedVessel;

			foreach (TrackingStationWidget widget in GetTrackingStationWidgets())
				widget.toggle.isOn = widget.vessel == selectedVessel;
		}

		private void ToolbarWindow(int windowID)
		{
			CurrentMode = (FilterMode)GUILayout.Toolbar((int)CurrentMode,
				FilterModeLabels, HighLogic.Skin.button,
				new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false) });
		}

		private void onVesselDestroy(Vessel vessel)
		{
			ResetVesselList();
			needsRerender = true;
		}

		private void onVesselCreate(Vessel vessel)
		{
			ResetVesselList();
		}

		private void onKnowledgeChanged(GameEvents.HostedFromToAction<IDiscoverable, DiscoveryLevels> data)
		{

			if ((data.to & DiscoveryLevels.Unowned) == DiscoveryLevels.Unowned && CurrentMode == FilterMode.Maneuver)
				CurrentMode = FilterMode.Default;

			ResetVesselList();
			needsRerender = true;
		}

		private void onMapViewFiltersModified(MapViewFiltering.VesselTypeFilter data)
		{
			needsWidgetColorRender = true;
		}

		private void ResetVesselList()
		{
			DefaultVessels = null;
		}

		private static Rect GetSideBarRect() =>
			((RectTransform)GameObject.Find("Side Bar").transform).rect;

		private static FieldInfo trackedVesselsField;
		private static FieldInfo vesselWidgetsField;
		private static FieldInfo GetSpaceTrackingField(string name) =>
			typeof(SpaceTracking).GetField(name,
			BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		private static FieldInfo vesselImageField;
		private static FieldInfo GetVesselIconSpriteField(string name) =>
			typeof(VesselIconSprite).GetField(name,
			BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		[System.Diagnostics.Conditional("DEBUG")]
		private void Log(string message) =>
			Debug.Log("ManeuverQueue: " + message);
		[System.Diagnostics.Conditional("DEBUG")]
		private void Log(string message, params object[] args) =>
			Log(args == null ? message : string.Format(message, args));
	}
}
