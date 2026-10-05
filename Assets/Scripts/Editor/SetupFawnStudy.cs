using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class SetupFawnStudy
{
    const string Directory = "Assets/Characters/Fawn/";
    [MenuItem("WildReach/Setup Forest Deer Routine")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Stop Play mode before setting up the deer.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.name!="ForestStudy")throw new System.Exception("Open ForestStudy first.");
        var idle=Import("Idle");var walk=Import("Walk");var run=Import("Hop");var grazing=Import("Grazing");var alert=Import("Alert");
        const string controllerPath=Directory+"FawnStudy.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters=new[]{new AnimatorControllerParameter{name="Speed",type=AnimatorControllerParameterType.Float},new AnimatorControllerParameter{name="Grazing",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="Alert",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="Spotted",type=AnimatorControllerParameterType.Bool}};
        var machine=controller.layers[0].stateMachine;foreach(var s in machine.states)machine.RemoveState(s.state);
        foreach(var oldTree in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>())Object.DestroyImmediate(oldTree,true);
        var tree=new BlendTree{name="Idle Walk Run",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);
        tree.AddChild(idle,0);tree.AddChild(walk,.8f);tree.AddChild(run,7f);
        var children=tree.children;var stride=children[1];stride.timeScale=1.94f;children[1]=stride;tree.children=children;
        var locomotion=machine.AddState("Locomotion");locomotion.motion=tree;machine.defaultState=locomotion;
        var graze=machine.AddState("Grazing");graze.motion=grazing;
        var lower=locomotion.AddTransition(graze);lower.hasExitTime=false;lower.duration=.35f;lower.hasFixedDuration=true;lower.AddCondition(AnimatorConditionMode.If,0,"Grazing");lower.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");
        var raise=graze.AddTransition(locomotion);raise.hasExitTime=false;raise.duration=.35f;raise.hasFixedDuration=true;raise.AddCondition(AnimatorConditionMode.IfNot,0,"Grazing");
        var scan=machine.AddState("Alert Scan");scan.motion=AlertClip(alert,false);
        var focus=machine.AddState("Alert Focus");focus.motion=AlertClip(alert,true);
        foreach(var state in new[]{scan,focus})
        {
            bool spotted=state==focus;
            var notice=machine.AddAnyStateTransition(state);notice.hasExitTime=false;notice.duration=.25f;notice.hasFixedDuration=true;notice.canTransitionToSelf=false;
            notice.AddCondition(AnimatorConditionMode.If,0,"Alert");notice.AddCondition(AnimatorConditionMode.Less,.08f,"Speed");notice.AddCondition(spotted?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Spotted");
            var calm=state.AddTransition(locomotion);calm.hasExitTime=false;calm.duration=.2f;calm.hasFixedDuration=true;calm.AddCondition(AnimatorConditionMode.IfNot,0,"Alert");
        }
        var go=GameObject.Find("Fawn | Idle Study") ?? GameObject.Find("Fawn | Forest Routine");
        if(!go)throw new System.Exception("Place the approved idle deer first.");
        var terrain=Terrain.activeTerrain;
        GameObject visual;
        if(go.GetComponent<Animator>())
        {
            visual=go;var point=visual.transform.position;var rotation=visual.transform.rotation;
            if(visual.GetComponent<FawnBrain>())Object.DestroyImmediate(visual.GetComponent<FawnBrain>());
            foreach(var component in visual.GetComponents<Component>())
                if(component is NavMeshAgent || component is Collider || component is Rigidbody)Object.DestroyImmediate(component);
            go=new GameObject("Fawn | Forest Routine");go.transform.position=new Vector3(point.x,terrain.SampleHeight(point)+terrain.transform.position.y,point.z);go.transform.rotation=rotation;
            visual.name="Fawn Visual";visual.transform.SetParent(go.transform,false);
        }
        else visual=go.GetComponentInChildren<Animator>().gameObject;
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Creatures/FawnIdleStudy.prefab");
        visual.transform.localScale=original.transform.localScale*1.35f;
        visual.transform.localRotation=Quaternion.identity;visual.transform.localPosition=Vector3.zero;
        var animator=visual.GetComponent<Animator>();
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        idle.SampleAnimation(visual,0);
        var bounds=Bounds(visual);visual.transform.position+=Vector3.up*(go.transform.position.y-bounds.min.y);
        var agent=go.GetComponent<NavMeshAgent>();if(!agent)agent=go.AddComponent<NavMeshAgent>();
        agent.enabled=false;agent.radius=.45f;agent.height=1.8f;agent.stoppingDistance=.4f;agent.speed=.8f;agent.acceleration=2.5f;agent.angularSpeed=130;agent.updateRotation=false;
        var collider=go.GetComponent<CapsuleCollider>();if(!collider)collider=go.AddComponent<CapsuleCollider>();
        collider.radius=.3f;collider.height=1.5f;collider.center=Vector3.up*.75f;
        var body=go.GetComponent<Rigidbody>();if(!body)body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        if(!go.GetComponent<FawnBrain>())go.AddComponent<FawnBrain>();
        var brainSettings=new SerializedObject(go.GetComponent<FawnBrain>());brainSettings.FindProperty("runSpeed").floatValue=7f;brainSettings.FindProperty("walkSpeed").floatValue=.8f;brainSettings.FindProperty("turnSpeed").floatValue=90f;brainSettings.ApplyModifiedPropertiesWithoutUndo();
        var nav=GameObject.Find("Forest Study Navigation") ?? new GameObject("Forest Study Navigation");
        var surface=nav.GetComponent<NavMeshSurface>();if(!surface)surface=nav.AddComponent<NavMeshSurface>();
        surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(0,1,12);surface.size=new Vector3(70,6,46);surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.12f;
        // The moving player is excluded from the static navigation bake.
        var player=Object.FindAnyObjectByType<PlayerMovement>();var modifier=player.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
        Physics.SyncTransforms();surface.BuildNavMesh();Object.DestroyImmediate(modifier);
        const string navPath="Assets/Scenes/ForestStudy_Navigation.asset";
        var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(old)AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData,navPath);
        if(!NavMesh.SamplePosition(go.transform.position,out var hit,2,NavMesh.AllAreas))throw new System.Exception("The deer spawn is outside the navigation surface.");
        agent.baseOffset=terrain.SampleHeight(hit.position)+terrain.transform.position.y-hit.position.y;
        go.transform.position=new Vector3(hit.position.x,hit.position.y+agent.baseOffset,hit.position.z);
        agent.enabled=true;
        PrefabUtility.SaveAsPrefabAsset(go,"Assets/Creatures/FawnForestStudy.prefab");
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Selection.activeGameObject=go;
        Debug.Log($"FAWN_ROUTINE_BUILT visualScale={visual.transform.localScale} visualOffset={visual.transform.localPosition} rootOffset={agent.baseOffset} navTriangles={NavMesh.CalculateTriangulation().indices.Length/3} clips={idle.length},{walk.length},{run.length},{grazing.length}");
    }
    static AnimationClip AlertClip(AnimationClip source,bool focus)
    {
        string path=Directory+(focus?"FawnAlertFocus.anim":"FawnAlertScan.anim");
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
        clip.ClearCurves();clip.name=focus?"Alert Focus":"Alert Scan";clip.frameRate=30;
        foreach(var binding in AnimationUtility.GetCurveBindings(source))
        {
            var original=AnimationUtility.GetEditorCurve(source,binding);var curve=new AnimationCurve();
            int end=focus?30:117;
            for(int frame=0;frame<=end;frame++)
            {
                float time=frame/30f;
                float value=focus?original.Evaluate(0):original.Evaluate(Mathf.Min(time,3.5f));
                if(!focus && time>3.5f)value=Mathf.Lerp(value,original.Evaluate(0),Mathf.SmoothStep(0,1,(time-3.5f)/.4f));
                curve.AddKey(time,value);
            }
            for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
    }
    static AnimationClip Import(string name)
    {
        string path=Directory+"Fawn"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.None;
        var clips=importer.defaultClipAnimations;foreach(var clip in clips){clip.name=name;clip.loopTime=name!="Alert";clip.loopPose=false;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
    }
    static Bounds Bounds(GameObject go)
    {
        var bounds=new Bounds();bool first=true;
        foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=renderer.sharedMesh;var weights=mesh.boneWeights;var vertices=mesh.vertices;
            var matrices=renderer.bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];
                var point=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
