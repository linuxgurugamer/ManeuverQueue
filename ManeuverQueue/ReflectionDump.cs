using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;
using KSP.IO;

using UnityEngine;
using UnityEngine.UI;

#if DEBUG
namespace ManeuverQueue
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    class ReflectionDump : MonoBehaviour
    {
        bool dumped = false;

        void DumpSpaceTracking()
        {
            int c = 0;
            foreach (var f in typeof(SpaceTracking).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Debug.Log("SpaceTracking - Field name[" + c.ToString() + "]: " + f.Name + "    Fieldtype: " + f.FieldType.ToString());
                c++;
            }
            c = 0;
            foreach (var f in typeof(VesselIconSprite).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Debug.Log("VesselIconSprite - Field name[" + c.ToString() + "]: " + f.Name + "    Fieldtype: " + f.FieldType.ToString());
                c++;
            }
        }

        void Start()
        {

        }
        void Update()
        {
            if (!dumped)
            {
                DumpSpaceTracking();
                dumped = true;
            }
        }
    }
}
#endif