using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class BuildForestGroundStudy
{
    const string Dir="Assets/Environment/Foliage3D/";
    static float Blend(float a,float b,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
    static float PathX(float z) => -1.6f + Mathf.Sin((z+4)*.25f)*.7f - 1.5f*Mathf.Exp(-(z-2)*(z-2)/8f);

    [MenuItem("WildReach/Build Forest Ground Study")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play before building the ground study.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/ForestStudy.unity") throw new Exception("Open ForestStudy first.");
        var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var data=terrain.terrainData;
        var earth=Layer("Study_Earth",Color.white);
        var moss=Layer("Study_Moss",new Color(.65f,1.65f,.8f));
        data.terrainLayers=new[]{earth,moss};
        int resolution=data.alphamapResolution;
        var map=new float[resolution,resolution,2];
        for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
        {
            float wx=terrain.transform.position.x+x/(float)(resolution-1)*data.size.x;
            float wz=terrain.transform.position.z+z/(float)(resolution-1)*data.size.z;
            float d=Mathf.Abs(wx-PathX(wz));
            float path=1-Blend(.75f,1.65f,d);
            float roots=1-Blend(1.0f,2.9f,new Vector2(wx,wz-2).magnitude);
            float mossWeight=Mathf.Clamp01(.58f+Mathf.PerlinNoise(wx*.25f+18,wz*.25f+18)*.4f-path*.85f-roots*.5f);
            map[z,x,0]=1-mossWeight; map[z,x,1]=mossWeight;
        }
        data.SetAlphamaps(0,0,map);
        var old=GameObject.Find("Ground Study | Root and Path");
        if(old) UnityEngine.Object.DestroyImmediate(old);
        var group=new GameObject("Ground Study | Root and Path");
        var shader=Shader.Find("WildReach/StudyGrass");
        if(!shader || !shader.isSupported) throw new Exception("Study grass shader must compile first.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Study_GrassBlade.mat");
        if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,Dir+"Study_GrassBlade.mat"); }
        material.shader=shader; material.enableInstancing=true;
        var rng=new System.Random(137);
        int count=0;
        for(float z=-7;z<9;z+=.24f) for(float x=-6;x<6;x+=.24f)
        {
            float wx=x+(float)rng.NextDouble()*.22f-.11f;
            float wz=z+(float)rng.NextDouble()*.22f-.11f;
            float pathDistance=Mathf.Abs(wx-PathX(wz));
            float rootDistance=new Vector2(wx,wz-2).magnitude;
            if(pathDistance<.85f || rootDistance<.85f) continue;
            float patch=Mathf.PerlinNoise(wx*.42f+31,wz*.42f+31);
            float density=Mathf.Lerp(.35f,.94f,Blend(.25f,.7f,patch));
            float edge=Mathf.Min(6-Mathf.Abs(wx),Mathf.Min(wz+7,9-wz));
            density*=Blend(0,1.5f,edge);
            if(rng.NextDouble()>density) continue;
            Vector3 pos=new Vector3(wx,terrain.SampleHeight(new Vector3(wx,0,wz))+terrain.transform.position.y-.015f,wz);
            if(Physics.Raycast(pos+Vector3.up*6,Vector3.down,out var hit,6.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider is not TerrainCollider && hit.point.y>pos.y+.08f) continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Environment/Grass/GrassClump_{1+rng.Next(3)}.prefab");
            var clump=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            clump.transform.SetParent(group.transform);
            clump.transform.position=pos;
            clump.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
            float height=.40f+(float)rng.NextDouble()*.5f;
            if(pathDistance<1.6f) height*=.7f;
            clump.transform.localScale=new Vector3(1.25f,height,1.25f);
            var renderer=clump.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off;
            count++;
        }
        EditorUtility.SetDirty(data); EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=group;
        Debug.Log($"GROUND_STUDY_READY {count} grass clumps; earth/moss terrain, open winding path.");
    }

    static readonly Vector2[][] Routes={
        new[]{new Vector2(-2,-12),new Vector2(-3,-6),new Vector2(-3,2),new Vector2(-5,9),new Vector2(-14,14),new Vector2(-24,16),new Vector2(-16,23),new Vector2(0,28),new Vector2(22,30),new Vector2(27,36)},
        new[]{new Vector2(-5,9),new Vector2(4,16),new Vector2(13,18),new Vector2(24,18),new Vector2(34,23)}
    };

    static float RouteDistance(Vector2 point)
    {
        float distance=float.PositiveInfinity;
        foreach(var route in Routes) for(int i=1;i<route.Length;i++)
        {
            var a=route[i-1]; var delta=route[i]-a;
            var closest=a+delta*Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude);
            distance=Mathf.Min(distance,Vector2.Distance(point,closest));
        }
        return distance;
    }

    [MenuItem("WildReach/Bring Forest Ground to Life")]
    public static void BuildFull()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play before painting the forest.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/ForestStudy.unity") throw new Exception("Open ForestStudy first.");
        var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var data=terrain.terrainData;
        foreach(string name in new[]{"Ground Study | Root and Path","Forest Floor | Living Ground"})
        { var old=GameObject.Find(name); if(old) UnityEngine.Object.DestroyImmediate(old); }
        var ground=new GameObject("Forest Floor | Living Ground");
        var trees=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t=>t.name.EndsWith("| Green Study") || t.name=="Whispering | 3D Canopy Study").ToList();
        var frame=new GameObject("Canopy edges"); frame.transform.SetParent(ground.transform);
        var whisper=trees.Find(t=>t.name=="Whispering | 3D Canopy Study");
        var winter=trees.Find(t=>t.name.StartsWith("Winter Skeleton"));
        Vector3[] edges={new Vector3(-30,0,-7),new Vector3(-17,0,-5),new Vector3(16,0,-5),new Vector3(30,0,-4),new Vector3(32,0,29),new Vector3(13,0,33),new Vector3(-8,0,33),new Vector3(-32,0,27)};
        for(int i=0;i<edges.Length;i++)
        {
            var source=i%3==0 && winter ? winter : whisper;
            var copy=UnityEngine.Object.Instantiate(source.gameObject,frame.transform);
            copy.name="Forest edge tree "+(i+1);
            copy.transform.position=edges[i]+Vector3.up*source.position.y;
            copy.transform.Rotate(0,i*137.5f,0,Space.World);
            copy.transform.localScale*=.8f+(i%4)*.1f;
            trees.Add(copy.transform);
        }
        var roots=trees.Select(t=>new Vector2(t.position.x,t.position.z)).ToArray();
        var layers=new[]{Layer("Study_Earth",Color.white),Layer("Study_Moss",new Color(.65f,1.65f,.8f)),Layer("Study_Meadow",new Color(.95f,1.35f,.65f))};
        data.terrainLayers=layers;
        int resolution=data.alphamapResolution;
        var map=new float[resolution,resolution,3];
        for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
        {
            var point=new Vector2(terrain.transform.position.x+x/(float)(resolution-1)*data.size.x,terrain.transform.position.z+z/(float)(resolution-1)*data.size.z);
            float path=1-Blend(.8f,1.8f,RouteDistance(point));
            float root=0;
            foreach(var centre in roots) root=Mathf.Max(root,1-Blend(.8f,3.1f,Vector2.Distance(point,centre)));
            float bare=Mathf.Clamp01(.04f+path*.96f+root*.58f+Mathf.Max(0,.38f-Mathf.PerlinNoise(point.x*.14f+4,point.y*.14f+4))*.8f);
            float sunny=Blend(.25f,.8f,Mathf.PerlinNoise(point.x*.085f+23,point.y*.085f+23))*(1-root);
            float green=1-bare;
            map[z,x,0]=1-green; map[z,x,1]=green*(1-sunny); map[z,x,2]=green*sunny;
        }
        data.SetAlphamaps(0,0,map);

        var plants=new GameObject("Logs, mushrooms and undergrowth"); plants.transform.SetParent(ground.transform);
        var logMaterial=CopyMaterial("Study_MossLog","Assets/Environment/MossLog/MossLog.mat");
        logMaterial.DisableKeyword("_NORMALMAP"); logMaterial.SetFloat("_Smoothness",0); logMaterial.SetColor("_EmissionColor",Color.white*.12f);
        Vector2[] logPoints={new Vector2(-7,3),new Vector2(4,11),new Vector2(18,23),new Vector2(-28,22),new Vector2(-9,27),new Vector2(-21,5)};
        var anchors=new List<Vector2>(roots.Take(5));
        for(int i=0;i<logPoints.Length;i++)
        {
            var log=Place("Assets/Environment/MossLog/MossLog.fbx",plants.transform,logPoints[i],.65f+(i%3)*.14f,i*67f,terrain);
            foreach(var r in log.GetComponentsInChildren<Renderer>()) r.sharedMaterial=logMaterial;
            anchors.Add(logPoints[i]);
        }
        Physics.SyncTransforms();
        var rng=new System.Random(839);
        int mushrooms=0,shrubs=0;
        foreach(var anchor in anchors)
        {
            for(int i=0;i<5;i++)
            {
                float angle=(float)rng.NextDouble()*Mathf.PI*2;
                float radius=1.7f+(float)rng.NextDouble()*3.4f;
                var point=anchor+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(RouteDistance(point)<1.65f || !FreeGround(point,terrain)) continue;
                var bush=Place(Dir+"FoliageCluster.fbx",plants.transform,point,.38f+(float)rng.NextDouble()*.42f,(float)rng.NextDouble()*360,terrain);
                bush.name="Leafy undergrowth "+(++shrubs);
                foreach(var r in bush.GetComponentsInChildren<Renderer>()) r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Foliage_Study.mat");
                for(int j=0;j<2;j++)
                {
                    var location=point+new Vector2((float)rng.NextDouble()-.5f,(float)rng.NextDouble()-.5f)*1.1f;
                    if(RouteDistance(location)<1.2f || !FreeGround(location,terrain)) continue;
                    var path=j%2==0 ? "Assets/Environment/Mushrooms/EmbercapCluster/EmbercapCluster_Glow.prefab" : "Assets/Environment/Mushrooms/EmbercapGrove/EmbercapGrove_Glow.prefab";
                    var mushroom=Place(path,plants.transform,location,.18f+(float)rng.NextDouble()*.22f,(float)rng.NextDouble()*360,terrain);
                    mushroom.name="Embercaps "+(++mushrooms);
                    foreach(var light in mushroom.GetComponentsInChildren<Light>()) UnityEngine.Object.DestroyImmediate(light.gameObject);
                }
            }
        }
        Physics.SyncTransforms();
        const int maskRes=256;
        var pixels=new Color32[maskRes*maskRes];
        int occupied=0;
        for(int z=0;z<maskRes;z++) for(int x=0;x<maskRes;x++)
        {
            var point=new Vector2(terrain.transform.position.x+x/(float)(maskRes-1)*data.size.x,terrain.transform.position.z+z/(float)(maskRes-1)*data.size.z);
            float path=RouteDistance(point); float root=roots.Min(c=>Vector2.Distance(c,point));
            int zone=0;
            float patch=Mathf.PerlinNoise(point.x*.32f+16,point.y*.32f+16);
            if(path>.95f && root>1.05f && patch>.19f && FreeGround(point,terrain))
            {
                float meadow=Mathf.PerlinNoise(point.x*.085f+23,point.y*.085f+23);
                zone=root<6 ? 2 : meadow>.56f ? 3 : 1;
                if(path<1.8f) zone=1;
                occupied++;
            }
            pixels[z*maskRes+x]=new Color32((byte)zone,0,0,255);
        }
        var mask=new Texture2D(maskRes,maskRes,TextureFormat.RGBA32,false,true); mask.SetPixels32(pixels); mask.Apply();
        const string maskPath=Dir+"Forest_GrassZones.png";
        File.WriteAllBytes(maskPath,mask.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(mask);
        AssetDatabase.ImportAsset(maskPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(maskPath);
        importer.isReadable=true; importer.sRGBTexture=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.SaveAndReimport();
        var meshes=Enumerable.Range(1,3).Select(i=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Environment/Grass/GrassClump_{i}.prefab").GetComponent<MeshFilter>().sharedMesh).ToArray();
        var shades=new[]{new Color(.4f,.68f,.24f),new Color(.29f,.53f,.22f),new Color(.59f,.7f,.27f)};
        for(int i=0;i<3;i++)
        {
            string matPath=Dir+$"Forest_Grass_{i}.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat) { mat=new Material(Shader.Find("WildReach/StudyGrass")); AssetDatabase.CreateAsset(mat,matPath); }
            mat.SetColor("_RootColor",new Color(.1f,.25f,.1f)); mat.SetColor("_TipColor",shades[i]); mat.enableInstancing=true;
            var go=new GameObject(new[]{"Short pathside grass","Shaded root grass","Sunlit meadow grass"}[i]); go.transform.SetParent(ground.transform);
            var field=go.AddComponent<GrassField>(); field.enabled=false;
            field.terrainName=terrain.name; field.zoneMap=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            field.meshes=meshes; field.material=mat; field.zoneGrass=new bool[3]; field.zoneGrass[i]=true;
            field.zoneDensity=new[]{.55f,.74f,.65f}; field.baseSpacing=.32f; field.drawDistance=42;
            field.seed=193; field.scaleRange=i==0 ? new Vector2(.35f,.65f) : i==1 ? new Vector2(.5f,.9f) : new Vector2(.6f,1f);
            field.scaleMultiplier=new Vector3(1.4f,1,1.4f); field.enabled=true; field.Refresh();
            EditorUtility.SetDirty(mat);
        }
        RenderSettings.fogDensity=.009f;
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=ground;
        Debug.Log($"FOREST_LIFE_READY trees={trees.Count} logs={logPoints.Length} shrubs={shrubs} mushrooms={mushrooms} grassCoverage={occupied/(float)pixels.Length:P0} instancedGrass=true");
    }

    static bool FreeGround(Vector2 point,Terrain terrain)
    {
        var origin=new Vector3(point.x,terrain.SampleHeight(new Vector3(point.x,0,point.y))+terrain.transform.position.y+2,point.y);
        return !Physics.Raycast(origin,Vector3.down,out var hit,2.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || hit.collider is TerrainCollider;
    }

    static GameObject Place(string asset,Transform parent,Vector2 point,float height,float yaw,Terrain terrain)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
        go.transform.SetParent(parent,false); go.transform.rotation=Quaternion.Euler(0,yaw,0);
        var renderers=go.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds;
        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.localScale*=height/bounds.size.y;
        bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position+=new Vector3(point.x-bounds.center.x,terrain.SampleHeight(new Vector3(point.x,0,point.y))+terrain.transform.position.y-bounds.min.y-.035f,point.y-bounds.center.z);
        return go;
    }

    static Material CopyMaterial(string name,string source)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+name+".mat");
        if(!mat) { mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(source)); AssetDatabase.CreateAsset(mat,Dir+name+".mat"); }
        EditorUtility.SetDirty(mat); return mat;
    }

    static TerrainLayer Layer(string name,Color tint)
    {
        var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(Dir+name+".terrainlayer");
        if(!layer) { layer=new TerrainLayer(); AssetDatabase.CreateAsset(layer,Dir+name+".terrainlayer"); }
        layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Textures/Forest.png");
        layer.tileSize=new Vector2(4,4); layer.normalMapTexture=null; layer.normalScale=0;
        layer.diffuseRemapMin=Vector4.zero; layer.diffuseRemapMax=new Vector4(tint.r,tint.g,tint.b,1);
        layer.smoothness=0; layer.metallic=0;
        EditorUtility.SetDirty(layer); return layer;
    }
}
