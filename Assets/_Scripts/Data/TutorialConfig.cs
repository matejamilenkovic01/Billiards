using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialStepKind
{
    Power = 0,          // straight in, the aim is set: only the pull matters
    Aim = 1,            // a cut: aim the ghost ball
    Release = 2,        // let go on green (easier bar)
    PowerControl = 3,   // a long straight shot: use only the power you need
    BallInHand = 4      // place the cue ball, then shoot
}

// How the release bar behaves in a step (v3 V9).
public enum ReleaseAssistMode
{
    AlwaysGreen = 0,    // no bar, every release is perfect
    Easy = 1,           // slower bar, wider green
    Normal = 2
}

// One lesson of the tutorial. The object ball sits ObjectDistance from the pocket along Approach (object -> pocket;
// zero = straight into the pocket's mouth); the cue ball stands CueDistance behind the ghost ball, turned by
// CutDegrees. So the layout fits any table size.
[Serializable]
public class TutorialStep
{
    public TutorialStepKind Kind;
    public string Title;
    [TextArea] public string Instruction;
    public string Success = "Nice!";
    [Header("Table")]
    [Range(0, 5)] public int Pocket = 5;
    public float ObjectDistance = 0.45f;
    public Vector2 Approach;
    public float CutDegrees;
    public float CueDistance = 0.55f;
    public int ObjectBall = 1;
    public int[] ExtraBalls = new int[0];
    public Vector2[] ExtraPositions = new Vector2[0];
    public bool BallInHand;
    [Header("Help")]
    public ReleaseAssistMode Assist = ReleaseAssistMode.Normal;
    [Tooltip("The cue starts on the right line (only the pull is taught).")]
    public bool PresetAim;
    public CoachTarget Coach = CoachTarget.None;
    public string CoachText;
    [Tooltip("Succeeds only at or under this pull (0-1).")]
    [Range(0f, 1f)] public float MaxPull = 1f;
    [Tooltip("Accept a Yellow release, not only Green.")]
    public bool YellowCounts = true;
    [Tooltip("After this many tries the lesson moves on anyway (0 = never).")]
    public int MoveOnAfter;
    [Tooltip("After this many misses the right aim is shown (0 = never).")]
    public int ShowAimAfter;
}

// The tutorial (v3 V9, gameplay G10): the lessons, how the bar is eased, the messages.
[CreateAssetMenu(fileName = "TutorialConfig", menuName = "Billiards/Tutorial Config")]
public class TutorialConfig : ScriptableObject
{
    public List<TutorialStep> Steps = new();

    [Header("Easy bar (Release lesson)")]
    public float EasyPeriodScale = 1.6f;
    public float EasyGreenScale = 1.5f;

    [Header("Feedback")]
    public float TooSoftPull = 0.3f;
    public float TooHardPull = 0.85f;
    [Tooltip("Seconds between a result and the next try or lesson.")]
    public float NextDelay = 1.8f;
    public string TooSoft = "Too soft — pull further";
    public string TooHard = "Too hard — try MED";
    public string Missed = "Almost! Try again";
    public string Scratch = "The cue ball went in — try again";
    public string RedRelease = "You let go in RED — the shot went off line";
    public string PowerTooHigh = "In — but that was a lot of power. Try less";
    public string ShowAim = "Here's the line. Now shoot!";
    public string Ready = "You're ready!";
}
