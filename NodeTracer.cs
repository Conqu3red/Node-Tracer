using System;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;


namespace NodeTracer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class NodeTracer : PolyTechMod
    {
        public new const string
            PluginGuid = "polytech.NodeTracer",
            PluginName = "Node Tracer",
            PluginVersion = "1.0.0";
        
        public static NodeTracer instance;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> toggleHotkey;
        public static ConfigEntry<Color> traceColor;
        public static ConfigEntry<int> TraceLength;
        Harmony harmony;
        void Awake()
        {
            this.repositoryUrl = "https://github.com/Conqu3red/Node-Tracer/"; // repo to check for updates from
			if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;
           
            modEnabled = Config.Bind("Node Tracer", "modEnabled", true, "Enable Mod");
            
            modEnabled.SettingChanged += onEnableDisable;

            toggleHotkey = Config.Bind("Node Tracer", "Toggle hotkey", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F9), "Keybind to toggle selected joints tracing");
            traceColor = Config.Bind("Node Tracer", "Trace color", Color.blue, "Color for selected nodes and traced nodes to appear as");
            TraceLength = Config.Bind("Node Tracer", "Trace length", 100, "How many frames to store traced data for");

            harmony = new Harmony("polytech.NodeTracer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            this.authors = new string[] {"Conqu3red"};

            PolyTechMain.registerMod(this);
        }

        public void Start(){
            // do something idk
        }


        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }
        public override void enableMod() 
        {
            modEnabled.Value = true;
        }
        public override void disableMod() 
        {
            modEnabled.Value = false;
        }

        public void Update(){
            if (toggleHotkey.Value.IsUp()){
                TraceManager.ToggleSelectedNodes();
            }
            
        }

        [HarmonyPatch(typeof(BridgePhysics), "FixedUpdateManual")]
        public static class UpdateDisplayPatch {
            public static void Postfix(){
                PolyPhysics.Viewers.GlDrawer.Clear();
                PolyPhysics.Viewers.GlDrawer.color = traceColor.Value;
                
                List<string> keys = new List<string>(TraceManager.nodeData.Keys);
                foreach (string guid in keys){
                    List<Vector3> positions = TraceManager.nodeData[guid];
                    BridgeJoint j = BridgeJoints.FindByGuid(guid);
                    if (j?.m_PhysicsNode != null){
                        positions.Add(j.m_PhysicsNode.pos);
                        if (positions.Count > Mathf.Max(2, TraceLength.Value)){
                            positions.RemoveAt(0);
                        }
                    }
                    if (!j){
                        TraceManager.nodeData.Remove(guid);
                        continue;
                    }
                    
                    Vector3 prevPos = new Vector3();
                    Vector3 pos;
                    for (int c = 0; c < positions.Count; c++){
                        pos = positions[c];
                        if (c != 0){
                            PolyPhysics.Viewers.GlDrawer.DrawLine(pos, prevPos);
                        }
                        prevPos = pos;

                    }
                }
            }
        }

        [HarmonyPatch]
        public static class GameStateSimChangePatch {
            static IEnumerable<MethodBase> TargetMethods(){
                yield return AccessTools.Method(typeof(GameStateSim), "Enter");
                yield return AccessTools.Method(typeof(GameStateSim), "Exit");
            }
            public static void Postfix(){
                List<string> keys = new List<string>(TraceManager.nodeData.Keys);
                foreach (string guid in keys){
                    TraceManager.nodeData[guid] = new List<Vector3>();
                }
            }
        }

        [HarmonyPatch(typeof(BridgeJoint), "SetColor")]
        public static class SetColorPatch {
            public static void Postfix(Color color, Color splitColor, BridgeJoint __instance){
                List<Vector3> l;
                if (TraceManager.nodeData.TryGetValue(__instance.m_Guid, out l)) color = traceColor.Value;
                
                if (__instance.m_IsAnchor)
		        {
		        	__instance.m_StaticIconFillLeft.color = color;
		        	__instance.m_StaticIconFillRight.color = color;
		        	__instance.m_StaticIconFillRightSplit.color = splitColor;
		        	return;
		        }
		        __instance.m_IconFillLeft.color = color;
		        __instance.m_IconFillRight.color = (__instance.m_IsSplit ? splitColor : color);
            }
        }
    
    }

    public static class TraceManager {
        public static Dictionary<string, List<Vector3>> nodeData = new Dictionary<string, List<Vector3>>();

        public static void ToggleNode(string guid){
            List<Vector3> l;
            if (nodeData.TryGetValue(guid, out l)){
                nodeData.Remove(guid);
            }
            else {
                nodeData[guid] = new List<Vector3>();
            }
        }

        public static void ToggleSelectedNodes(){
            foreach (BridgeJoint j in BridgeSelectionSet.m_Joints){
                ToggleNode(j.m_Guid);
            }
        }
    }
}