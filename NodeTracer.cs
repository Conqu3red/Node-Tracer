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
using System.Diagnostics;

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
            PluginVersion = "1.0.3";
        
        public static NodeTracer instance;
        public static ConfigEntry<bool> modEnabled, traceAllSplitParts, keepTraceLinesAfterSimEnd;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> toggleHotkey, selectAllEnabledHotkey, clearTracesHotKey;
        public static ConfigEntry<Color> traceColor, split2Color, split3Color;
        public static ConfigEntry<int> TraceLength;
        public static ConfigEntry<float> LineWidth;
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

            toggleHotkey = Config.Bind(PluginName, "Toggle tracer on selected nodes", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F9), "Keybind to toggle tracing selected joints");
            selectAllEnabledHotkey = Config.Bind(PluginName, "Select all joints that will be traced", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F10), "Keybind to select every joint that will be traced");
            clearTracesHotKey = Config.Bind(PluginName, "Clear trace lines", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None), "Keybind to clear trace lines");
            traceColor = Config.Bind(PluginName, "Trace color", Color.blue, "Color for selected nodes and traced nodes to appear as");
            split2Color = Config.Bind(PluginName, "Trace color (2nd split parts)", Color.blue, "Color for selected nodes that split as 2 to appear as");
            split3Color = Config.Bind(PluginName, "Trace color (3rd split parts)", Color.blue, "Color for selected nodes that split as 3 to appear as");
            
            TraceLength = Config.Bind(PluginName, "Trace length", 100, "How many frames to store traced data for");
            LineWidth = Config.Bind(PluginName, "Line Width", 0.05f, "Width of traced lines");
            traceAllSplitParts = Config.Bind(PluginName, "Trace all split parts", true, "Toggle for whether to trace all split parts or just the original node");
            keepTraceLinesAfterSimEnd = Config.Bind(PluginName, "Keep Trace Lines after sim end", false);
            
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

        public void SelectJointsThatWillBeTraced(){
            BridgeSelectionSet.CancelSelection();
            foreach (NodeTraceInfo t in TraceManager.nodeData.Values){
                BridgeJoint j = BridgeJoints.FindByGuid(t.nodeGuid);
                if (j){
                    BridgeSelectionSet.SelectJointAndConnectedEdges(j);
                }
            }
            BridgeSelectionSet.DeSelectAllEdges();
        }

        public void ClearTraceLines(){
            foreach (NodeTraceInfo n in TraceManager.nodeData.Values){
                n.ClearLines();
            }
            foreach (VehicleTraceInfo info in TraceManager.vehicleData.Values){
                info.ClearLines();
            }
        }

        public void Update(){
            if (toggleHotkey.Value.IsUp()){
                TraceManager.ToggleSelectedNodes();
                TraceManager.ToggleSelectedVehicle();

            }
            if (selectAllEnabledHotkey.Value.IsUp() && GameStateManager.GetState() == GameState.BUILD)
                SelectJointsThatWillBeTraced();
            
            if (clearTracesHotKey.Value.IsUp()){
                ClearTraceLines();
            }

            
            List<string> trace_keys = new List<string>(TraceManager.nodeData.Keys);
            foreach (string guid in trace_keys){
                BridgeJoint j = BridgeJoints.FindByGuid(guid);
                if (!j || !j.isActiveAndEnabled){
                    //instance.Logger.LogInfo("class Update found dead TraceInfo");
                    TraceManager.ToggleNode(guid); // remove nodes that no longer exist
                    continue;
                }
            }
            
            trace_keys = new List<string>(TraceManager.vehicleData.Keys);
            foreach (string guid in trace_keys){
                Vehicle v = Vehicles.FindByGuid(guid);
                if (v == null || !v.isActiveAndEnabled){
                    TraceManager.ToggleVehicle(v);
                    continue;
                }
            }
		}
		    
        /*
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
        */

        [HarmonyPatch(typeof(BridgePhysics), "FixedUpdateManual")]
        public static class UpdateDisplayPatch {
            public static void Postfix(){
                //PolyPhysics.Viewers.GlDrawer.Clear();
                var stopwatch = Stopwatch.StartNew();
                List<string> keys = new List<string>(TraceManager.nodeData.Keys);
                foreach (string guid in keys){
                    NodeTraceInfo info = TraceManager.nodeData[guid];
                    BridgeJoint j = BridgeJoints.FindByGuid(guid);
                    if (!j || !j.isActiveAndEnabled){
                        //instance.Logger.LogInfo("BridgePhysics found dead TraceInfo");
                        TraceManager.ToggleNode(guid);
                        continue;
                    }
                    info.UpdateManual();
                }
                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;
                //instance.Logger.LogInfo($"Update Nodes in {ts.TotalMilliseconds} ms");
            }
        }

        [HarmonyPatch]
        public static class GameStateSimChangePatch {
            static IEnumerable<MethodBase> TargetMethods(){
                yield return AccessTools.Method(typeof(GameStateSim), "Enter");
                yield return AccessTools.Method(typeof(GameStateSim), "Exit");
            }
            public static void Postfix(MethodInfo __originalMethod){
                //instance.Logger.LogInfo(__originalMethod.Name);
                if (__originalMethod.Name == "Exit" && keepTraceLinesAfterSimEnd.Value) return;
                foreach (NodeTraceInfo t in TraceManager.nodeData.Values){
                    t.ClearLines();
                    t.splitNodes.Clear();
                }
                foreach (VehicleTraceInfo info in TraceManager.vehicleData.Values){
                    info.ClearLines();
                }
            }
        }

        [HarmonyPatch(typeof(BridgeJoint), "SetColor")]
        public static class SetColorPatch {
            public static void Postfix(Color color, Color splitColor, BridgeJoint __instance){
                //if (NodeTracer.instance.icon3Split == null) NodeTracer.instance.createTripleSplitTextures(__instance);
                NodeTraceInfo l;
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
                NodeTraceInfo t = TraceManager.findByPhysicsNode(src);
                if (t == null) return;
                NodeTraceInfo _t = new NodeTraceInfo(t.color, t.split2_color, t.split3_color, t.width, duplicate);
                _t.splitPart = duplicatePart;
                _t.UpdateLineColor();
                t.splitNodes.Add(_t);
            }
        }

        [HarmonyPatch(typeof(PolyPhysics.Vehicle), "Execute")]
        public static class VehicleStep {
            public static void Postfix(PolyPhysics.Vehicle __instance, PolyPhysics.Rigidbody[] ___wheels){
                VehicleTraceInfo t = TraceManager.FindByPhysicsVehicle(__instance);
                if (t == null) return;
                t.UpdateManual(___wheels);
            }
        }
    
    
        // Trace Classes:
        public class TraceInfo {
            public virtual void Destroy(){}
            public virtual void ClearLines(){}
            public static Vector3 offset = new Vector3(0, 0, 10f);
        }

        public class NodeTraceInfo : TraceInfo {
            public void CreateRenderer()
            {
                currentLine = new GameObject();
                lineRenderer = currentLine.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
                UpdateLineColor();
            }
            public NodeTraceInfo(){
                CreateRenderer();
            }
            public NodeTraceInfo(Color color, Color split2_color, Color split3_color, float width, string nodeGuid){
                this.color = color;
                this.split2_color = split2_color;
                this.split3_color = split3_color;
                this.width = width;
                this.nodeGuid = nodeGuid;
                CreateRenderer();
            }
            public NodeTraceInfo(Color color, Color split2_color, Color split3_color, float width, PolyPhysics.Node splitNode){
                this.color = color;
                this.split2_color = split2_color;
                this.split3_color = split3_color;
                this.width = width;
                this.splitNode = splitNode;
                CreateRenderer();
            }

            public override void Destroy(){
                //instance.Logger.LogInfo("destruct NodeTraceInfo");
                UnityEngine.Object.Destroy(currentLine);
                foreach (NodeTraceInfo t in splitNodes)
                    t.Destroy();
            }

            public override void ClearLines()
            {
                points.Clear();
                pushLinesToRenderer();
                foreach (NodeTraceInfo t in splitNodes){
                    t.ClearLines();
                }
            }

            public void UpdateHistory(){
                PolyPhysics.Node node = BridgeJoints.FindByGuid(nodeGuid)?.m_PhysicsNode;
                if (node == null) node = splitNode;
                if (node != null){
                    points.Add((Vector3)node.pos + node_offset);
                    if (points.Count > Mathf.Max(2, NodeTracer.TraceLength.Value)){
                        // remove oldest lines
                        points.RemoveRange(0, points.Count - NodeTracer.TraceLength.Value);
                    }
                    pushLinesToRenderer();
                }
            }
            public void UpdateManual(){
                UpdateHistory();
                
                if (NodeTracer.traceAllSplitParts.Value){
                    foreach (NodeTraceInfo t in splitNodes){
                        t.UpdateManual();
                    }
                }
            }
            
            public void pushLinesToRenderer()
            {
                lineRenderer.positionCount = points.Count;
                lineRenderer.SetPositions(points.ToArray());
            }

            public void UpdateLineColor(){
                var c = color;
                if (splitPart == PolyPhysics.Part.B) c = split2_color;
                if (splitPart == PolyPhysics.Part.C) c = split3_color;
                if (lineRenderer.startColor != c)
                {
                    lineRenderer.startColor = c;
                    lineRenderer.endColor = c;
                }
            }
            public Color color;
            public Color split2_color;
            public Color split3_color;
            public float width;
            private GameObject currentLine;
            private LineRenderer lineRenderer;
            private List<Vector3> points = new List<Vector3>();
            public string nodeGuid;
            public PolyPhysics.Node splitNode;
            public List<NodeTraceInfo> splitNodes = new List<NodeTraceInfo>();
            public PolyPhysics.Part splitPart = PolyPhysics.Part.All;
            public static Vector3 node_offset = new Vector3(0,0,-1.5f);
        }
        public class VehicleTraceInfo : TraceInfo {
            public VehicleTraceInfo(){
            }
            public VehicleTraceInfo(Color color, float width, string vehicleGuid){
                this.color = color;
                this.width = width;
                this.vehicleGuid = vehicleGuid;
            }

            public LineRenderer makeLineRenderer()
            {
                var currentObj = new GameObject();
                gameObjects.Add(currentObj);
                var lr = currentObj.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startWidth = width;
                lr.endWidth = width;
                lr.startColor = color;
                lr.endColor = color;
                return lr;
            }
            public override void Destroy(){
                foreach (var currentObj in gameObjects)
                    UnityEngine.Object.Destroy(currentObj);
            }

            public override void ClearLines(){
                foreach (var line in wheelTracers){
                    line.positionCount = 0;
                }
                foreach (var points in wheelPoints)
                {
                    points.Clear();
                }
            }

            public void pushLinesToRenderer(LineRenderer lineRenderer, List<Vector3> points)
            {
                lineRenderer.positionCount = points.Count;
                lineRenderer.SetPositions(points.ToArray());
            }

            public void UpdateManual(PolyPhysics.Rigidbody[] wheels){
                for (int i = 0; i < wheels.Length; i++){
                    if (wheelTracers.Count <= i){
                        int amount_to_add = i - wheelTracers.Count + 1;
                        for (int j = 0; j < amount_to_add; j++){
                            wheelTracers.Add(makeLineRenderer());
                            wheelPoints.Add(new List<Vector3>());
                        }
                    }
                    
                    wheelPoints[i].Add(wheels[i].t2.position);
                    
                    if (wheelPoints[i].Count > Mathf.Max(2, NodeTracer.TraceLength.Value)){
                        wheelPoints[i].RemoveRange(0, wheelPoints[i].Count - NodeTracer.TraceLength.Value);
                    }
                    pushLinesToRenderer(wheelTracers[i], wheelPoints[i]);
                }
            }

            public Color color;
            public float width;
            public string vehicleGuid;
            public List<GameObject> gameObjects = new List<GameObject>();
            public List<LineRenderer> wheelTracers = new List<LineRenderer>();
            public List<List<Vector3>> wheelPoints = new List<List<Vector3>>();
        }

        public static class TraceManager {
            public static Dictionary<string, NodeTraceInfo> nodeData = new Dictionary<string, NodeTraceInfo>();
            public static Dictionary<string, VehicleTraceInfo> vehicleData = new Dictionary<string, VehicleTraceInfo>();
            public static void ToggleNode(string guid){
                NodeTraceInfo l;
                if (nodeData.TryGetValue(guid, out l)){
                    l.Destroy();
                    nodeData.Remove(guid);
                }
                else {
                    nodeData[guid] = new NodeTraceInfo(
                        traceColor.Value, 
                        split2Color.Value, 
                        split3Color.Value, 
                        LineWidth.Value,
                        guid
                    );
                }
            }

            public static void ToggleSelectedNodes(){
                foreach (BridgeJoint j in BridgeSelectionSet.m_Joints){
                    ToggleNode(j.m_Guid);
                }
            }

            public static NodeTraceInfo findByPhysicsNode(PolyPhysics.Node node){
                foreach (NodeTraceInfo t in nodeData.Values){
                    BridgeJoint j = BridgeJoints.FindByGuid(t.nodeGuid);
                    if (j?.m_PhysicsNode == node) return t;
                }
                return null;
            }

            public static void ToggleVehicle(Vehicle vehicle){
                VehicleTraceInfo l;
                if (vehicleData.TryGetValue(vehicle.m_Guid, out l)){
                    l.Destroy();
                    vehicleData.Remove(vehicle.m_Guid);
                }
                else {
                    vehicleData[vehicle.m_Guid] = new VehicleTraceInfo(
                        traceColor.Value,
                        LineWidth.Value,
                        vehicle.m_Guid
                    );
                }
            }

            public static void ToggleSelectedVehicle(){
                if (GameStateBuild.m_HoverSandboxItem != null)
                {
                    Vehicle vehicle = null;
                    if (GameStateBuild.m_HoverSandboxItem.m_Type == SandboxItemType.VEHICLE)
                    {
                        vehicle = GameStateBuild.m_HoverSandboxItem.GetComponent<Vehicle>();
                    }
                    if (vehicle != null){
                        ToggleVehicle(vehicle);
                    }
                }
            }

            public static VehicleTraceInfo FindByPhysicsVehicle(PolyPhysics.Vehicle vehicle){
                foreach (VehicleTraceInfo t in vehicleData.Values){
                    Vehicle j = Vehicles.FindByGuid(t.vehicleGuid);
                    if (j?.Physics == vehicle) return t;
                }
                return null;
            }
        }
    }
}
