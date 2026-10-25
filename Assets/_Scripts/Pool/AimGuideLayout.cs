using UnityEngine;

public enum GuideBandKind
{
    Object = 0,          // the hit ball, from its far edge along ObjectDir
    CueAfterBall = 1,    // the cue ball after the contact, along its (curved) path
    CueAfterCushion = 2  // the cue ball after its first cushion, along its simulated path
}

// What the aim guide shows, as table-plane geometry (implementation_definition_v3 V1). One rule for every case:
// a thin line for the cue ball's centre up to the first obstacle (ghost ball, cushion contact or pocket), then
// only bands of the cue's arrow length (ObjectGuideDiameters x 2R). No second bounce, no second ball: a band
// after a cushion stops at whatever it meets. From v4 the line and the cue ball's bands follow the simulated path
// (squirt, masse, spin off the ball and the cushion). Preallocated, so building it every frame allocates nothing.
public sealed class AimGuideLayout
{
    public const int MaxBands = 2;

    public sealed class Band
    {
        public GuideBandKind Kind;
        public readonly Vector2[] Points = new Vector2[ShotPredictor.MaxPathPoints];
        public int Count;
        public float Length;
    }

    public readonly Band[] Bands = { new(), new() };
    public int BandCount;
    public bool HasLine;
    public Vector2 LineStart;
    public Vector2 LineEnd;
    // The line as a polyline (curved by squirt and masse), from LineStart to LineEnd.
    public readonly Vector2[] LinePoints = new Vector2[ShotPredictor.MaxPathPoints];
    public int LineCount;
    public bool HasGhost;
    public Vector2 GhostPoint;
    public int HitBall;
    // An opponent's ball (or the 8 too early) is the first ball touched.
    public bool Illegal;
    // The cue ball drops: straight into a pocket, or into one after the cushion.
    public bool Scratch;
    public Vector2 ScratchPoint;

    public void Build(in Prediction prediction, BallState[] balls, float radius, float arrowLength)
    {
        BandCount = 0;
        HasGhost = false;
        Illegal = false;
        Scratch = false;
        HitBall = prediction.HitBall;

        // The line starts at the cue ball's front edge, not under it.
        BuildLine(prediction, radius);

        switch (prediction.Hit)
        {
            case PredictionHit.Ball:
                HasGhost = true;
                GhostPoint = prediction.Point;
                Illegal = !prediction.IsLegal;
                if (Illegal)
                    return;
                Vector2 farEdge = balls[prediction.HitBall].Pos + prediction.ObjectDir * radius;
                AddStraight(GuideBandKind.Object, farEdge, prediction.ObjectDir, arrowLength);
                if (prediction.CuePath != null && prediction.CuePathCount >= 2)
                    AddPath(GuideBandKind.CueAfterBall, prediction.CuePath, prediction.CuePathCount);
                // The cue ball follows the hit ball into a pocket within the arrow: scratch.
                Scratch = prediction.SecondHit == PredictionHit.Pocket;
                ScratchPoint = prediction.SecondPoint;
                break;

            case PredictionHit.Cushion:
                if (prediction.SecondHit == PredictionHit.Ball)
                    Illegal = !prediction.SecondIsLegal;
                Scratch = prediction.SecondHit == PredictionHit.Pocket;
                ScratchPoint = prediction.SecondPoint;
                if (prediction.CuePath != null && prediction.CuePathCount >= 2)
                    AddPath(GuideBandKind.CueAfterCushion, prediction.CuePath, prediction.CuePathCount);
                else
                    AddStraight(GuideBandKind.CueAfterCushion, prediction.Point, prediction.BounceDir, arrowLength);
                break;

            case PredictionHit.Pocket:
                Scratch = true;
                ScratchPoint = prediction.Point;
                break;
        }
    }

    private void BuildLine(in Prediction prediction, float radius)
    {
        LineCount = 0;
        LineStart = prediction.Start;
        LineEnd = prediction.Point;
        if (prediction.Line == null || prediction.LineCount < 2)
        {
            LinePoints[LineCount++] = prediction.Start;
            LinePoints[LineCount++] = prediction.Point;
        }
        else
        {
            for (int i = 0; i < prediction.LineCount && LineCount < LinePoints.Length; i++)
                LinePoints[LineCount++] = prediction.Line[i];
        }

        // Drop the part under the cue ball: walk `radius` along the polyline.
        float skip = radius;
        int first = 0;
        while (first < LineCount - 1)
        {
            float segment = (LinePoints[first + 1] - LinePoints[first]).magnitude;
            if (segment > skip)
            {
                LinePoints[first] += (LinePoints[first + 1] - LinePoints[first]) * (skip / segment);
                break;
            }
            skip -= segment;
            first++;
        }
        HasLine = first < LineCount - 1;
        if (first > 0)
        {
            for (int i = first; i < LineCount; i++)
                LinePoints[i - first] = LinePoints[i];
            LineCount -= first;
        }
        if (HasLine)
            LineStart = LinePoints[0];
    }

    private void AddStraight(GuideBandKind kind, Vector2 from, Vector2 direction, float length)
    {
        if (length <= 1e-4f || BandCount >= MaxBands)
            return;
        Band band = Bands[BandCount++];
        band.Kind = kind;
        band.Points[0] = from;
        band.Points[1] = from + direction.normalized * length;
        band.Count = 2;
        band.Length = length;
    }

    private void AddPath(GuideBandKind kind, Vector2[] points, int count)
    {
        if (BandCount >= MaxBands)
            return;
        Band band = Bands[BandCount];
        band.Kind = kind;
        band.Count = Mathf.Min(count, band.Points.Length);
        band.Length = 0f;
        for (int i = 0; i < band.Count; i++)
        {
            band.Points[i] = points[i];
            if (i > 0)
                band.Length += (points[i] - points[i - 1]).magnitude;
        }
        if (band.Length > 1e-4f)
            BandCount++;
    }
}
