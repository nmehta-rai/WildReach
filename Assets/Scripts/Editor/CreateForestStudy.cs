using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class CreateForestStudy
{
    const string ScenePath = "Assets/Scenes/ForestStudy.unity";
    const string FoliageDir = "Assets/Environment/Foliage3D/";

    [MenuItem("WildReach/Open Forest Study")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        AssetDatabase.Refresh();
        var source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var camera = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        if (!player || !camera) throw new Exception("Original player and camera must be available.");
        var playerRoot = player.transform.root.gameObject;
        var cameraRoot = camera.transform.root.gameObject;
        var patch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.MoveGameObjectToScene(playerRoot, patch);
        SceneManager.MoveGameObjectToScene(cameraRoot, patch);
        foreach (var root in source.GetRootGameObjects())
            if (root.GetComponentInChildren<Canvas>(true) || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true))
                SceneManager.MoveGameObjectToScene(root, patch);
        EditorSceneManager.CloseScene(source, true);
        SceneManager.SetActiveScene(patch);
        foreach (var s in UnityEngine.Object.FindObjectsByType<SaveSystem>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(s);
        var spawn = new GameObject("Study Spawn"); spawn.transform.position = new Vector3(0, 1.05f, -6);
        player.transform.position = spawn.transform.position; player.transform.rotation = Quaternion.identity;
        var respawn = player.GetComponent<RespawnController>();
        if (respawn) { var so = new SerializedObject(respawn); so.FindProperty("respawnPoint").objectReferenceValue = spawn.transform; so.ApplyModifiedPropertiesWithoutUndo(); }
        camera.SetTarget(player.transform);
        foreach(var swim in playerRoot.GetComponentsInChildren<SwimSystem>()) swim.SetWaterLevel(-50);
        var cam = camera.GetComponent<Camera>();
        cam.transform.position = new Vector3(0, 3.2f, -11); cam.transform.LookAt(new Vector3(0, 3, 1));
        cam.farClipPlane = 150; cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        new GameObject("Study Controls").AddComponent<PauseMenu>();

        var data = new TerrainData { heightmapResolution = 129, size = new Vector3(24, 3, 24) };
        var heights = new float[129, 129];
        for (int z=0; z<129; z++) for (int x=0; x<129; x++)
        {
            float wx=x/128f*24-12, wz=z/128f*24-12;
            float edge=Mathf.Clamp01((Mathf.Sqrt(wx*wx+wz*wz)-7)/6);
            heights[z,x]=edge*.08f*(.5f+.5f*Mathf.PerlinNoise(wx*.12f+7,wz*.12f+7));
        }
        data.SetHeights(0,0,heights); AssetDatabase.CreateAsset(data,"Assets/Scenes/ForestStudy_Terrain.asset");
        var ground = Terrain.CreateTerrainGameObject(data); ground.name="Forest Study | 24m Patch"; ground.transform.position=new Vector3(-12,0,-12);
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(groundMat,FoliageDir+"Study_Ground.mat");
        var layer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Materials/Grass_Temperate.terrainlayer"));
        layer.normalMapTexture=null; layer.normalScale=0; AssetDatabase.CreateAsset(layer,FoliageDir+"Study_Grass.terrainlayer");
        data.terrainLayers=new[]{layer};
        ground.GetComponent<Terrain>().materialTemplate=groundMat;

        var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/ForestTrees/ForestTree.prefab"));
        tree.name="Whispering | 3D Canopy Study";
        PrefabUtility.UnpackPrefabInstance(tree, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var oldCanopy=tree.transform.Find("Canopy");
        var anchors=oldCanopy.Cast<Transform>().Select(t=>t.position).ToArray();
        UnityEngine.Object.DestroyImmediate(oldCanopy.gameObject);
        var bounds=BoundsOf(tree);
        
        float scale=10f/bounds.size.y;
        tree.transform.localScale*=scale;
        bounds=BoundsOf(tree); tree.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,2-bounds.center.z);
        var foliageMat=Material("Foliage_Study",Color.white);
        foliageMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(FoliageDir+"Foliage_Base.png"));
        foliageMat.SetFloat("_Cull",0); foliageMat.SetFloat("_Smoothness",0); foliageMat.enableInstancing=true;
        var canopy=new GameObject("3D Foliage"); canopy.transform.SetParent(tree.transform,false);
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(FoliageDir+"FoliageCluster.fbx");
        for (int i=0;i<anchors.Length;i++)
        {
            var leaf=(GameObject)PrefabUtility.InstantiatePrefab(asset); leaf.name=$"Cluster {i+1:00}";
            leaf.transform.SetParent(canopy.transform,false);
            // Reuse crown distribution from the old tree, with actual mesh clusters.
            leaf.transform.position=anchors[i]*scale+tree.transform.position;
            leaf.transform.rotation=Quaternion.Euler((i%3-1)*18,i*137.508f,(i%5-2)*8);
            leaf.transform.localScale=Vector3.one*(Mathf.Max(bounds.size.x,bounds.size.z)*.33f/scale);
            foreach(var r in leaf.GetComponentsInChildren<Renderer>()) r.sharedMaterial=foliageMat;
            leaf.transform.localScale*=1.8f/BoundsOf(leaf).size.y;
        }
        foreach(var filter in tree.GetComponentsInChildren<MeshFilter>())
            if(!filter.transform.IsChildOf(canopy.transform) && !filter.GetComponent<Collider>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        var sun=new GameObject("Morning Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.color=new Color(1,.92f,.77f); sun.intensity=1.3f; sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(42,-35,0);
        RenderSettings.sun=sun; RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.5f,.68f,.74f); RenderSettings.ambientEquatorColor=new Color(.37f,.46f,.34f); RenderSettings.ambientGroundColor=new Color(.21f,.25f,.17f);
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.62f,.78f,.8f); RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.007f;
        var sky=new Material(Shader.Find("Skybox/Procedural")); sky.SetColor("_SkyTint",new Color(.5f,.7f,.85f)); sky.SetFloat("_AtmosphereThickness",.8f); AssetDatabase.CreateAsset(sky,FoliageDir+"Study_Sky.mat"); RenderSettings.skybox=sky;
        var volume=new GameObject("Study Atmosphere").AddComponent<Volume>(); volume.isGlobal=true;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>(); profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral); profile.Add<ColorAdjustments>().saturation.Override(8); AssetDatabase.CreateAsset(profile,FoliageDir+"Study_Profile.asset"); volume.sharedProfile=profile;
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(patch,ScenePath);
        Debug.Log($"STUDY_READY {ScenePath}, {anchors.Length} clusters, canopy triangles {anchors.Length*3324}");
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,false)}).ToArray();
        Selection.activeGameObject=tree;
    }
    [MenuItem("WildReach/Refine Forest Study Tree")]
    public static void RefineTree()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before saving tree changes.");
        var tree=GameObject.Find("Whispering | 3D Canopy Study");
        DressTree(tree);
        var camera=UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        var clearance=camera.GetComponent<FoliageViewClearance>() ?? camera.gameObject.AddComponent<FoliageViewClearance>();
        clearance.SetTarget(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject=tree;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("TREE_REFINED root buried 0.18m; camera foliage clearance enabled");
    }
    static void DressTree(GameObject tree, float canopyStart=.39f)
    {
        var canopy=tree.transform.Find("3D Foliage");
        var trunkRenderers=tree.GetComponentsInChildren<Renderer>().Where(r=>(!canopy || !r.transform.IsChildOf(canopy))).ToArray();
        var trunkBounds=trunkRenderers[0].bounds; foreach(var r in trunkRenderers) trunkBounds.Encapsulate(r.bounds);
        float ground=Terrain.activeTerrain.SampleHeight(tree.transform.position)+Terrain.activeTerrain.transform.position.y;
        tree.transform.position+=Vector3.up*(ground-.18f-trunkBounds.min.y);
        trunkBounds.center+=Vector3.up*(ground-.18f-trunkBounds.min.y);
        var points=new List<Vector3>();
        foreach(var filter in tree.GetComponentsInChildren<MeshFilter>().Where(f=>(!canopy || !f.transform.IsChildOf(canopy))))
        {
            using(var meshData=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
            using(var verts=new NativeArray<Vector3>(meshData[0].vertexCount,Allocator.Temp))
            {
                meshData[0].GetVertices(verts);
                foreach(var v in verts) points.Add(filter.transform.TransformPoint(v));
            }
        }
        if(canopy) UnityEngine.Object.DestroyImmediate(canopy.gameObject);
        float unit=trunkBounds.size.y/10f;
        var crown=new GameObject("3D Foliage"); crown.transform.SetParent(tree.transform,false);
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(FoliageDir+"FoliageCluster.fbx");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(FoliageDir+"Foliage_Study.mat");
        mat.shader=Shader.Find("WildReach/ClimbableFoliage"); mat.SetFloat("_Cull",0); EditorUtility.SetDirty(mat);
        int count=0;
        var clusterBounds=new List<Bounds>();
        void AddCluster(Vector3 position, float height, Quaternion rotation)
        {
            var cluster=(GameObject)PrefabUtility.InstantiatePrefab(model);
            cluster.name=$"Crown Foliage {++count:00}";
            cluster.transform.SetParent(crown.transform,false);
            cluster.transform.position=position;
            cluster.transform.rotation=rotation;
            cluster.transform.localScale*=height/BoundsOf(cluster).size.y;
            foreach(var r in cluster.GetComponentsInChildren<Renderer>()) r.sharedMaterial=mat;
            clusterBounds.Add(BoundsOf(cluster));
        }
        for(int tier=0;tier<6;tier++) for(int sector=0;sector<8;sector++)
        {
            float height=trunkBounds.min.y+trunkBounds.size.y*Mathf.Lerp(canopyStart,.915f,tier/5f);
            Vector3 best=Vector3.zero; float bestRadius=0;
            foreach(var p in points)
            {
                if(Mathf.Abs(p.y-height)>.47f*unit) continue;
                var off=p-trunkBounds.center;
                float angle=(Mathf.Atan2(off.z,off.x)*Mathf.Rad2Deg+360)%360;
                if((int)(angle/45)!=sector) continue;
                float radius=new Vector2(off.x,off.z).magnitude;
                if(radius>bestRadius) { bestRadius=radius; best=p; }
            }
            if(bestRadius<.65f*unit) continue;
            AddCluster(best-Vector3.up*.12f*unit, (tier<2 ? 2.1f : 2.5f)*unit,
                Quaternion.Euler(12+(tier%3)*8,sector*45+tier*19,(sector%3-1)*12));
        }
        // Overlapping inner lobes connect the branch-tip clumps into one crown.
        float innerRadius=Mathf.Max(.65f*unit,Mathf.Max(trunkBounds.size.x,trunkBounds.size.z)*.16f);
        for(int tier=0;tier<4;tier++)
        {
            float height=trunkBounds.min.y+trunkBounds.size.y*Mathf.Lerp(canopyStart+.19f,.91f,tier/3f);
            for(int sector=0;sector<3;sector++)
            {
                float angle=(sector*120+tier*47)*Mathf.Deg2Rad;
                var position=new Vector3(trunkBounds.center.x+Mathf.Cos(angle)*innerRadius,
                    height,trunkBounds.center.z+Mathf.Sin(angle)*innerRadius);
                AddCluster(position,2.6f*unit,Quaternion.Euler(20,sector*120+tier*47,12));
            }
        }
        // Cover exposed crown tips, including the highest central twig.
        foreach(var tip in points.Where(p=>p.y>trunkBounds.min.y+trunkBounds.size.y*.84f)
            .OrderByDescending(p=>p.y))
        {
            bool covered=clusterBounds.Any(b=>
                Mathf.Abs(tip.x-b.center.x)<b.extents.x*.65f &&
                Mathf.Abs(tip.y-b.center.y)<b.extents.y*.65f &&
                Mathf.Abs(tip.z-b.center.z)<b.extents.z*.65f);
            if(!covered) AddCluster(tip+Vector3.up*.15f*unit,2.25f*unit,Quaternion.Euler(18,count*137.508f,8));
        }
        Debug.Log($"TREE_DRESSED {tree.name}: {count} dimensional clusters");
    }

    [MenuItem("WildReach/Build Tree Skeleton Study")]
    public static void BuildSkeletonStudy()
    {
        if(EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before building tree specimens."); return; }
        if(SceneManager.GetActiveScene().path!=ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var terrain=Terrain.activeTerrain;
        terrain.terrainData.size=new Vector3(72,3,48);
        terrain.transform.position=new Vector3(-36,0,-12);
        terrain.name="Forest Study | Tree Specimens";
        EditorUtility.SetDirty(terrain.terrainData);
        var existing=GameObject.Find("Tree Skeleton Studies");
        if(existing) UnityEngine.Object.DestroyImmediate(existing);
        var group=new GameObject("Tree Skeleton Studies");
        string[] paths={
            "Assets/Environment/ForestTrees/WinterSkeleton/WinterSkeleton.fbx",
            "Assets/Environment/Trees/AncientSentinel/Meshy_AI_Ancient_Sentinel_0606030025_texture.fbx",
            "Assets/Environment/MotherTree/Meshy_AI_The_Hollowroot_Tree_0529211539_texture.fbx",
            "Assets/Environment/SpiralTree/SpiralTree.fbx",
            "Assets/Mid-Tree-1.fbx",
            "Assets/MotherTree.fbx"
        };
        string[] names={"Winter Skeleton","Ancient Sentinel","Hollowroot","Spiral Tree","Ruin Tree","Mother Tree"};
        string[] materials={
            "Assets/Environment/ForestTrees/WinterSkeleton/WinterSkeleton.mat",
            "Assets/Environment/Trees/AncientSentinel/AncientSentinel_Bark.mat",
            "Assets/Environment/MotherTree/MotherTree_Mat.mat",
            "Assets/Environment/SpiralTree/SpiralTree.mat",null,null
        };
        float[] heights={10,12,14,8,10,12};
        for(int i=0;i<paths.Length;i++)
        {
            var root=new GameObject(names[i]+" | Green Study"); root.transform.SetParent(group.transform);
            var trunk=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
            trunk.transform.SetParent(root.transform,false);
            PrefabUtility.UnpackPrefabInstance(trunk,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var bounds=BoundsOf(root); root.transform.localScale*=heights[i]/bounds.size.y;
            bounds=BoundsOf(root);
            root.transform.position+=new Vector3((i%3-1)*24-bounds.center.x,-bounds.min.y,12+i/3*18-bounds.center.z);
            var bark=materials[i]!=null ? AssetDatabase.LoadAssetAtPath<Material>(materials[i]) : null;
            foreach(var r in trunk.GetComponentsInChildren<Renderer>())
            {
                if(bark) r.sharedMaterials=Enumerable.Repeat(bark,r.sharedMaterials.Length).ToArray();
                else
                {
                    var converted=r.sharedMaterials.Select(source=>
                    {
                        string path=FoliageDir+names[i].Replace(" ","")+"_"+(source ? source.name.Replace(" ","") : "Bark")+".mat";
                        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!m)
                        {
                            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            if(source && source.mainTexture) m.SetTexture("_BaseMap",source.mainTexture);
                            m.SetColor("_BaseColor",source ? source.color : new Color(.5f,.4f,.3f));
                            m.SetFloat("_Smoothness",0); AssetDatabase.CreateAsset(m,path);
                        }
                        return m;
                    }).ToArray();
                    r.sharedMaterials=converted;
                }
            }
            foreach(var filter in trunk.GetComponentsInChildren<MeshFilter>())
            {
                var collider=filter.GetComponent<MeshCollider>();
                if(!collider) collider=filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=filter.sharedMesh;
            }
            DressTree(root,i==4 ? .60f : .39f);
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject=group;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("SKELETON_STUDY_READY 7 distinct trees including approved Whispering; current green cluster shared");
    }

    static Material Material(string name, Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",0); AssetDatabase.CreateAsset(m,FoliageDir+name+".mat"); return m;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>(); var b=rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds); return b;
    }
}
