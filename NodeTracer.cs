using System;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Linq;

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
            PluginVersion = "1.0.1";
        
        public static NodeTracer instance;
        public static ConfigEntry<bool> modEnabled, traceAllSplitParts;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> toggleHotkey, selectAllEnabledHotkey;
        public static ConfigEntry<Color> traceColor, split2Color, split3Color;
        public static ConfigEntry<int> TraceLength;
        Harmony harmony;
        public Sprite icon3Split;
        void Awake()
        {
            this.repositoryUrl = "https://github.com/Conqu3red/Node-Tracer/"; // repo to check for updates from
			if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;
           
            modEnabled = Config.Bind(PluginName, "Mod Enabled", true, "Enable Mod");
            
            modEnabled.SettingChanged += onEnableDisable;

            toggleHotkey = Config.Bind(PluginName, "Toggle hotkey", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F9), "Keybind to toggle selected joints tracing");
            selectAllEnabledHotkey = Config.Bind(PluginName, "Select all joints that will be traced", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F10), "Keybind to select every joint that will be traced");
            traceColor = Config.Bind(PluginName, "Trace color", Color.blue, "Color for selected nodes and traced nodes to appear as");
            split2Color = Config.Bind(PluginName, "Trace color (2nd split parts)", Color.blue, "Color for selected nodes that split as 2 to appear as");
            split3Color = Config.Bind(PluginName, "Trace color (3rd split parts)", Color.blue, "Color for selected nodes that split as 3 to appear as");
            
            TraceLength = Config.Bind(PluginName, "Trace length", 100, "How many frames to store traced data for");
            traceAllSplitParts = Config.Bind(PluginName, "Trace all split parts", true, "Toggle for whether to trace all split parts or just the original node");

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
            if (selectAllEnabledHotkey.Value.IsUp() && GameStateManager.GetState() == GameState.BUILD){
                foreach (TraceInfo t in TraceManager.nodeData.Values){
                    BridgeJoint j = BridgeJoints.FindByGuid(t.nodeGuid);
                    BridgeSelectionSet.CancelSelection();
                    if (j){
                        BridgeSelectionSet.SelectJointAndConnectedEdges(j);
                    }
                }
                BridgeSelectionSet.DeSelectAllEdges();
            }
            
        }

        public void createTripleSplitTextures(BridgeJoint j){
            // horrible code to read from a unreadable texture (j.m_Split3_B.sprite.texture)
            // https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary( 
                j.m_Split3_B.sprite.texture.width,
                j.m_Split3_B.sprite.texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(j.m_Split3_B.sprite.texture, tmp);
            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;
            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;
            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(j.m_Split3_B.sprite.texture.width, j.m_Split3_B.sprite.texture.height, TextureFormat.ARGB32, false);
            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            // Reset the active RenderTexture
            RenderTexture.active = previous;
            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);
            // "myTexture2D" now has the same pixels from "j.m_Split3_B.sprite.texture" and it's readable.
            Color[] pixels = myTexture2D.GetPixels();
            Color[] newPixels = pixels.Select(color => color = new Color(1,1,1,1)).ToArray();
            myTexture2D.SetPixels(newPixels);
            myTexture2D.Apply();
            icon3Split = Sprite.Create(
                myTexture2D, 
                j.m_Split3_B.sprite.rect, 
                j.m_Split3_B.sprite.pivot
            );
            //j.m_Split3_B.sprite = icon3Split;
            Logger.LogInfo(icon3Split.texture.GetPixels().Length);
        }

        [HarmonyPatch(typeof(BridgePhysics), "FixedUpdateManual")]
        public static class UpdateDisplayPatch {
            public static void Postfix(){
                PolyPhysics.Viewers.GlDrawer.Clear();
                
                List<string> keys = new List<string>(TraceManager.nodeData.Keys);
                foreach (string guid in keys){
                    TraceInfo info = TraceManager.nodeData[guid];
                    BridgeJoint j = BridgeJoints.FindByGuid(guid);
                    if (!j){
                        TraceManager.nodeData.Remove(guid);
                        continue;
                    }
                    info.UpdateManual();
                    info.Draw();
                    foreach (TraceInfo splitPart in info.splitNodes){
                        info.UpdateManual();
                        splitPart.Draw();
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
                    BridgeJoint j = BridgeJoints.FindByGuid(guid);
                    TraceManager.nodeData[guid].history = new List<Vector3>();
                    TraceManager.nodeData[guid].splitNodes.Clear();
                }
            }
        }

        [HarmonyPatch(typeof(BridgeJoint), "SetColor")]
        public static class SetColorPatch {
            public static void Postfix(Color color, Color splitColor, BridgeJoint __instance){
                //if (NodeTracer.instance.icon3Split == null) NodeTracer.instance.createTripleSplitTextures(__instance);
                TraceInfo l;
                if (TraceManager.nodeData.TryGetValue(__instance.m_Guid, out l)){
                    color = l.color;
                    //splitColor = l.split2_color;
                }
                
                
                if (__instance.m_IsAnchor)
		        {
		        	__instance.m_StaticIconFillLeft.color = color;
		        	__instance.m_StaticIconFillRight.color = color;
		        	__instance.m_StaticIconFillRightSplit.color = splitColor;
		        	return;
		        }
		        __instance.m_IconFillLeft.color = color;
		        __instance.m_IconFillRight.color = (__instance.m_IsSplit ? splitColor : color);
                // it doesn't make sense!
                // Setting the SpriteRenderer color doesn't give accurate color
                // setting the texture causes nothing to appear! why?
            }
        }

        [HarmonyPatch(typeof(PolyPhysics.HydraulicController), "RegisterNode_Part_Split")]
        public static class NodeSplitPatch {
            public static void Postfix(PolyPhysics.Node src, PolyPhysics.Node duplicate, PolyPhysics.Part duplicatePart){
                if (!NodeTracer.traceAllSplitParts.Value || !modEnabled.Value) return;
                TraceInfo t = TraceManager.findByPhysicsNode(src);
                if (t == null) return;
                TraceInfo _t = new TraceInfo(t.color, t.split2_color, t.split3_color, duplicate);
                _t.splitPart = duplicatePart;
                t.splitNodes.Add(_t);
            }
        }
    
    }

    public class TraceInfo {
        public TraceInfo(){
            this.history = new List<Vector3>();
        }
        public TraceInfo(Color color, Color split2_color, Color split3_color, string nodeGuid){
            this.color = color;
            this.split2_color = split2_color;
            this.split3_color = split3_color;
            this.history = new List<Vector3>();
            this.nodeGuid = nodeGuid;
        }
        public TraceInfo(Color color, Color split2_color, Color split3_color, PolyPhysics.Node splitNode){
            this.color = color;
            this.split2_color = split2_color;
            this.split3_color = split3_color;
            this.history = new List<Vector3>();
            this.splitNode = splitNode;
        }
        public void UpdateManual(){
            PolyPhysics.Node node = BridgeJoints.FindByGuid(nodeGuid)?.m_PhysicsNode;
            if (node == null) node = splitNode;
            if (node != null){
                history.Add(node.pos);
                if (history.Count > Mathf.Max(2, NodeTracer.TraceLength.Value)){
                   history.RemoveRange(0, history.Count - NodeTracer.TraceLength.Value);
                }
                if (NodeTracer.traceAllSplitParts.Value){
                    foreach (TraceInfo t in splitNodes){
                        t.UpdateManual();
                    }
                }
            }
        }
        public void Draw(){
            Vector3 prevPos = new Vector3();
            Vector3 pos;
            PolyPhysics.Viewers.GlDrawer.color = color;
            if (splitPart == PolyPhysics.Part.B) PolyPhysics.Viewers.GlDrawer.color = split2_color;
            if (splitPart == PolyPhysics.Part.C) PolyPhysics.Viewers.GlDrawer.color = split3_color;
            for (int c = 0; c < history.Count; c++){
                pos = history[c];
                if (c != 0){
                    PolyPhysics.Viewers.GlDrawer.DrawLine(pos, prevPos);
                }
                prevPos = pos;
            }
        }
        public Color color;
        public Color split2_color;
        public Color split3_color;
        public List<Vector3> history;
        public string nodeGuid;
        public PolyPhysics.Node splitNode;
        public List<TraceInfo> splitNodes = new List<TraceInfo>();
        public PolyPhysics.Part splitPart;
    }

    public static class TraceManager {
        public static Dictionary<string, TraceInfo> nodeData = new Dictionary<string, TraceInfo>();
        public static void ToggleNode(string guid){
            TraceInfo l;
            if (nodeData.TryGetValue(guid, out l)){
                nodeData.Remove(guid);
            }
            else {
                nodeData[guid] = new TraceInfo(
                    NodeTracer.traceColor.Value, 
                    NodeTracer.split2Color.Value, 
                    NodeTracer.split3Color.Value, 
                    guid
                );
            }
        }

        public static void ToggleSelectedNodes(){
            foreach (BridgeJoint j in BridgeSelectionSet.m_Joints){
                ToggleNode(j.m_Guid);
            }
        }

        public static TraceInfo findByPhysicsNode(PolyPhysics.Node node){
            foreach (TraceInfo t in nodeData.Values){
                BridgeJoint j = BridgeJoints.FindByGuid(t.nodeGuid);
                if (j?.m_PhysicsNode == node) return t;
            }
            return null;
        }
    }
}