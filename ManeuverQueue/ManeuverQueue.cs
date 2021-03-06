﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;
using KSP.IO;

using UnityEngine;
using UnityEngine.UI;

using ClickThroughFix;

namespace FatHand
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class ManeuverQueue : MonoBehaviour
    {

        protected const double minimumManeuverDeltaT = 15.0 * 60.0;
        const float WINDOW_VERTICAL_POSITION = 36;

        public static GUIContent[] FilterModeLabels = new GUIContent[] {
            new GUIContent("MET", "Shows the default Tracking Station list"),
             new GUIContent("MNV", "Shows only those ships with maneuvers nodes, order by next maneuver node time (earliest first)"),
             new GUIContent("A-Z", "Shows the default list, sorted alphabetically")
        };
        public enum FilterMode
        {
            Undefined = -1,
            Default,
            Maneuver,
            Name
        };

        protected static SpaceTracking spaceTrackingScene;
        protected static VesselIconSprite vesselIconSprite;

        protected Rect windowPos;
        protected GUIStyle windowStyle;
        protected bool delaySetMode;
        protected bool render;
        protected bool needsRerender;
        protected bool needsWidgetColorRender;
        protected Color nodePassedColor = new Color(255.0f / 255, 58.0f / 255, 58.0f / 255, 1);
        protected Color nodeWarningColor = new Color(255.0f / 255, 255.0f / 255, 58.0f / 255, 1);

        private FilterMode _currentMode = FilterMode.Undefined;


        public static int TRACKEDVESSELS = -1;
        public static int VESSELWIDGETS = -1;
        public static int IMAGE = -1;

        public bool InitOffsets()
        {
            TRACKEDVESSELS = -1;
            VESSELWIDGETS = -1;
            IMAGE = -1;

            int c = 0;
            foreach (var f in typeof(SpaceTracking).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                switch (f.Name)
                {
                    case "trackedVessels":
                        TRACKEDVESSELS = c; break;
                    case "vesselWidgets":
                        VESSELWIDGETS = c; break;
                }
                c++;
            }
            c = 0;
            foreach (var f in typeof(VesselIconSprite).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                switch (f.Name)
                {
                    case "image":
                        IMAGE = c; break;
                }
                c++;
            }
            if (TRACKEDVESSELS >= 0 && VESSELWIDGETS >= 0 && IMAGE >= 0)
                return true;
            return false;
        }



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

            if (!InitOffsets())
            {
                var s ="ManeuverQueue not compatible with this KSP version";
                Debug.Log("[ManeuverQueue]: " + s);
                ScreenMessages.PostScreenMessage(s, 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            spaceTrackingScene = FindObjectOfType<SpaceTracking>();
            vesselIconSprite = FindObjectOfType <VesselIconSprite > ();
            if (vesselIconSprite == null)
                vesselIconSprite = new VesselIconSprite();

            trackedVesselsField = Refl.GetField(spaceTrackingScene, TRACKEDVESSELS);
            vesselWidgetsField = Refl.GetField(spaceTrackingScene, VESSELWIDGETS);
            vesselImageField = Refl.GetField(vesselIconSprite, IMAGE);
            if (trackedVesselsField == null || vesselWidgetsField == null)
            {
                var s = "ManeuverQueue: Could not get trackedVessels/ vesselWidgets FieldInfo, plugin will be disabled";
                Debug.Log(s);
                ScreenMessages.PostScreenMessage(s, 5, ScreenMessageStyle.UPPER_CENTER);
                return;
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
                GetCurrentMode();
            }
            else
            {
                delaySetMode = true;
                Log("Could not get vessels in Start(), delaying");
            }

            render = true;

            Log("Start Finished");
        }

        void GetCurrentMode()
        {
            delaySetMode = false;
            CurrentMode = (FilterMode)pluginConfiguration.GetValue(configurationModeKey, (int)FilterMode.Default);
        }

        protected void Update()
        {
            if (trackedVesselsField == null)
                return; //won't work

            if (delaySetMode)
            {
                Log("Delayed set mode");
                GetCurrentMode();
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
        /// <summary>
        /// 
        /// 

        string tooltip = "";
        bool drawTooltip = true;
        // Vector2 mousePosition;
        Vector2 tooltipSize;
        float tooltipX, tooltipY;
        Rect tooltipRect;
        void SetupTooltip()
        {
            Vector2 mousePosition;
            mousePosition.x = Input.mousePosition.x;
            mousePosition.y = Screen.height - Input.mousePosition.y;
            //  Log.Info("SetupTooltip, tooltip: " + tooltip);
            if (tooltip != null && tooltip.Trim().Length > 0)
            {
                tooltipSize = HighLogic.Skin.label.CalcSize(new GUIContent(tooltip));
                tooltipX = (mousePosition.x + tooltipSize.x > Screen.width) ? (Screen.width - tooltipSize.x) : mousePosition.x;
                tooltipY = mousePosition.y;
                if (tooltipX < 0) tooltipX = 0;
                if (tooltipY < 0) tooltipY = 0;
                tooltipRect = new Rect(tooltipX - 1, tooltipY - tooltipSize.y, tooltipSize.x + 4, tooltipSize.y);
            }
        }

        void TooltipWindow(int id)
        {
            //if (HighLogic.CurrentGame.Parameters.CustomParams<CCOLParams>().tooltips)
                GUI.Label(new Rect(2, 0, tooltipRect.width - 2, tooltipRect.height), tooltip, HighLogic.Skin.label);
        }


        /// 
        /// </summary>
        protected void OnGUI()
        {
            if (render)
            {
                windowPos = ClickThruBlocker.GUILayoutWindow(1, windowPos, ToolbarWindow, "", windowStyle, new GUILayoutOption[0]);

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


                
                if (HighLogic.CurrentGame.Parameters.CustomParams<MQ>().tooltips && tooltip != null && tooltip != "")
                {
                    SetupTooltip();
                    ClickThruBlocker.GUIWindow(1234, tooltipRect, TooltipWindow, "");
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

            if (Event.current.type == EventType.Repaint && GUI.tooltip != tooltip)
            {
                tooltip = GUI.tooltip;
            }
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

       
        private static FieldInfo GetSpaceTrackingField2(string name) => Refl.GetField(spaceTrackingScene, TRACKEDVESSELS);

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
