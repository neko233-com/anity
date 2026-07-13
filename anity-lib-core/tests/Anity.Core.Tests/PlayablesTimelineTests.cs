using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Playables / PlayableDirector / Timeline — ≥12 cases.</summary>
public class PlayablesTimelineTests
{
    [Fact]
    public void PlayableGraph_Create_Valid()
    {
        var g = PlayableGraph.Create("t");
        Assert.True(g.IsValid());
        Assert.False(g.IsPlaying());
        g.Destroy();
        Assert.False(g.IsValid());
    }

    [Fact]
    public void PlayableGraph_Play_Evaluate_AdvancesTime()
    {
        var g = PlayableGraph.Create();
        g.Play();
        Assert.True(g.IsPlaying());
        g.Evaluate(0.5f);
        Assert.Equal(0.5, g.GetTime(), 3);
        g.SetTime(2.0);
        Assert.Equal(2.0, g.GetTime(), 3);
        g.Stop();
        Assert.False(g.IsPlaying());
        Assert.Equal(0, g.GetTime());
        g.Destroy();
    }

    [Fact]
    public void ScriptPlayable_Create_Behaviour()
    {
        var g = PlayableGraph.Create();
        var sp = ScriptPlayable<TestPlayableBehaviour>.Create(g);
        Assert.True(sp.IsValid());
        Assert.NotNull(sp.GetBehaviour());
        Playable p = sp;
        Assert.True(p.IsValid());
        g.Destroy();
    }

    [Fact]
    public void ScriptPlayableOutput_SetSource()
    {
        var g = PlayableGraph.Create();
        var sp = ScriptPlayable<TestPlayableBehaviour>.Create(g);
        var output = ScriptPlayableOutput.Create(g, "out");
        output.SetSourcePlayable(sp);
        Assert.True(output.Source.IsValid());
        g.Destroy();
    }

    [Fact]
    public void PlayableDirector_PlayPauseStop()
    {
        var go = new GameObject("dir");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.duration = 3;
        dir.Play();
        Assert.Equal(PlayState.Playing, dir.state);
        dir.Pause();
        Assert.Equal(PlayState.Paused, dir.state);
        dir.Stop();
        Assert.Equal(0, dir.time);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void PlayableDirector_Events()
    {
        var go = new GameObject("dir2");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        int played = 0, paused = 0, stopped = 0;
        dir.played += _ => played++;
        dir.paused += _ => paused++;
        dir.stopped += _ => stopped++;
        dir.Play();
        dir.Pause();
        dir.Stop();
        Assert.Equal(1, played);
        Assert.Equal(1, paused);
        Assert.Equal(1, stopped);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void PlayableDirector_Evaluate_Hold()
    {
        var go = new GameObject("dir3");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.duration = 1.0;
        dir.extrapolationMode = DirectorWrapMode.Hold;
        dir.timeUpdateMode = DirectorUpdateMode.Manual;
        dir.Play();
        dir.Evaluate(2.0f);
        Assert.Equal(1.0, dir.time, 3);
        Assert.Equal(PlayState.Playing, dir.state); // Hold keeps playing state
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void PlayableDirector_Evaluate_Loop()
    {
        var go = new GameObject("dir4");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.duration = 1.0;
        dir.extrapolationMode = DirectorWrapMode.Loop;
        dir.timeUpdateMode = DirectorUpdateMode.Manual;
        dir.Play();
        dir.Evaluate(1.5f);
        Assert.InRange(dir.time, 0.0, 1.0);
        Assert.Equal(PlayState.Playing, dir.state);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void PlayableDirector_Evaluate_None_Stops()
    {
        var go = new GameObject("dir5");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.duration = 0.5;
        dir.extrapolationMode = DirectorWrapMode.None;
        dir.timeUpdateMode = DirectorUpdateMode.Manual;
        dir.Play();
        dir.Evaluate(1.0f);
        Assert.Equal(PlayState.Paused, dir.state);
        Assert.Equal(0.5, dir.time, 3);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void TimelineAsset_TracksAndClips()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        asset.SetDuration(10);
        var anim = asset.CreateTrack<AnimationTrack>("Anim");
        var clip = anim.CreateDefaultClip();
        clip.start = 1;
        clip.duration = 2;
        Assert.Equal(1, asset.outputTrackCount);
        Assert.Equal(1, anim.clipCount);
        Assert.True(clip.ContainsTime(1.5));
        Assert.False(clip.ContainsTime(0.5));
        Assert.Equal(3, clip.end);
    }

    [Fact]
    public void TimelineAsset_DurationFromClips()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        asset.SetDuration(1);
        var t = asset.CreateTrack<ActivationTrack>("Act");
        var c = t.CreateDefaultClip();
        c.start = 0;
        c.duration = 7;
        Assert.Equal(7, asset.duration);
    }

    [Fact]
    public void TimelineAsset_GetClipsAt_RespectsMute()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        var t = asset.CreateTrack<AudioTrack>("Aud");
        var c = t.CreateDefaultClip();
        c.start = 0;
        c.duration = 5;
        Assert.Single(asset.GetClipsAt(1));
        t.muted = true;
        Assert.Empty(asset.GetClipsAt(1));
    }

    [Fact]
    public void PlayableDirector_WithTimelineAsset()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        asset.SetDuration(4);
        var track = asset.CreateTrack<AnimationTrack>();
        track.CreateDefaultClip().duration = 4;

        var go = new GameObject("timelineDir");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.playableAsset = asset;
        Assert.Equal(4, dir.duration);
        dir.Play();
        Assert.True(dir.playableGraph.IsValid());
        dir.Evaluate(0.1f);
        Assert.True(dir.time >= 0);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void PlayableDirector_RebuildGraph()
    {
        var go = new GameObject("rebuild");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        var g1 = dir.playableGraph;
        dir.RebuildGraph();
        Assert.True(dir.playableGraph.IsValid());
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void DirectorUpdateMode_AndWrapEnums()
    {
        Assert.Equal(0, (int)DirectorUpdateMode.DSPClock);
        Assert.Equal(3, (int)DirectorUpdateMode.Manual);
        Assert.Equal(1, (int)DirectorWrapMode.Loop);
        Assert.Equal(1, (int)PlayState.Playing);
    }

    [Fact]
    public void Timeline_DeleteTrackAndClip()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        var t = asset.CreateTrack<ControlTrack>("c");
        var clip = t.CreateDefaultClip();
        t.DeleteClip(clip);
        Assert.Equal(0, t.clipCount);
        asset.DeleteTrack(t);
        Assert.Equal(0, asset.outputTrackCount);
    }

    private sealed class TestPlayableBehaviour
    {
        public int tick { get; set; }
    }
}
