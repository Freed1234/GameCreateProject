#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Disposable-project batch regression: stale per-frame enemy lists must be safe.
[InitializeOnLoad]
public static class MobHealthBarsCheck
{
    const string Key="MobHealthBarsCheck";
    static MobHealthBarsCheck()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>
        {
            if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false))
            {SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Pending",true);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Refresh(MobHealthBars bars)=>typeof(MobHealthBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
    static int Count(MobHealthBars bars)=>((IDictionary)typeof(MobHealthBars).GetField("bars",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bars)).Count;
    static EnemyHealth Spawn(Transform container)
    {
        var go=new GameObject("Lifetime test enemy");go.transform.SetParent(container);
        var enemy=go.AddComponent<EnemyHealth>();enemy.InitializeSpawnHealth(3);return enemy;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("CHECK_PASS: "+message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key+"Pending",false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Key+"Pending",false);
        try
        {
            var container=new GameObject("Enemies").AddComponent<EnemySpawner>();container.enabled=false;
            var bars=new GameObject("Bars").AddComponent<MobHealthBars>();
            var enemy=Spawn(container.transform);
            var cached=SkillUtil.EnemiesNow(container.transform);Refresh(bars);
            Check(Count(bars)==1,"living enemy receives health bar");
            // Deterministically retain a destroyed Unity object in this frame's cache.
            Object.DestroyImmediate(enemy.gameObject);Refresh(bars);
            Check(Count(bars)==0,"destroyed cached enemy is safe and its bar is removed");
            cached.Clear();enemy=Spawn(container.transform);cached.Add(enemy);Refresh(bars);
            enemy.gameObject.SetActive(false);Refresh(bars);
            Check(Count(bars)==0,"inactive cached enemy bar removed");
            cached.Clear();enemy=Spawn(container.transform);cached.Add(enemy);Refresh(bars);
            typeof(EnemyHealth).GetField("currentHealth",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(enemy,0);
            Refresh(bars);Check(Count(bars)==0,"dead cached enemy bar removed before destruction");
            cached.Clear();enemy=Spawn(container.transform);cached.Add(enemy);Refresh(bars);
            Check(Count(bars)==1,"subsequent living enemies still update");
            enemy.gameObject.AddComponent<BossHitProxy>();Refresh(bars);
            Check(Count(bars)==0,"boss proxy remains excluded");
            Debug.Log("MOB_HEALTH_BARS_LIFETIME_PASS");SessionState.SetInt(Key+"Exit",0);
        }
        catch(Exception e){Debug.LogException(e);}
        finally{EditorApplication.ExitPlaymode();}
    }
}
#endif
