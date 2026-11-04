using Operations_Control.Communication;
using System;
using System.Collections.Generic;
using System.Threading;
using Operations_Control.Models;
using Operations_Control.Motion;

namespace Operations_Control.Application
{
    public sealed class CanvasFollower
    {
        public const double DefaultDelayMs = 100;
        public const double StartToleranceMm = 0.5;
        private const double SampleMs = 60, ContourToleranceMm = 0.02, MaximumInputAgeMs = 15000;
        private sealed class PendingPoint
        {
            public MotionPoint Point;
            public double TimeMs;
            public PendingPoint(MotionPoint point, double timeMs) { Point = point; TimeMs = timeMs; }
        }
        private sealed class Segment
        {
            public MotionPoint End, Velocity;
            public double Duration;
        }

        private readonly IControllerSession session;
        private readonly Func<double> now;
        private readonly Queue<PendingPoint> points = new Queue<PendingPoint>();
        private int generation, xMotor, yMotor, coordinateSystem, stepsRemaining, sourceSteps;
        private bool pending, configured, streaming, streamStarted, hasFallback;
        private InterpolationMode mode;
        private decimal speed;
        private decimal ySpeed;
        private bool automaticSpeed;
        private double delayMs, lastInputTime, executionEnd, retryAt, sourceDuration;
        private double maximumXAcceleration = 100000, maximumYAcceleration = 100000;
        private MotionPoint latestFallback, lastPoint, lastSubmitted, lastVelocity, sourceStep;
        private bool hasSubmitted;
        private CanvasSettings mapping;
        private PendingPoint source, lookahead;
        private MotionSegment sourceCurve;
        private Segment prepared;
        public bool Active { get; private set; }
        public bool WaitingForBuffer { get; private set; }
        public int PendingPointCount { get { return points.Count + (source == null ? 0 : 1) + (lookahead == null ? 0 : 1); } }
        public event EventHandler Failed;
        public string Error { get; private set; }
        public double MouseSpeedMmPerSecond { get; private set; }
        public double FollowSpeedMmPerSecond { get; private set; }
        public bool SpeedLimited { get; private set; }

        public CanvasFollower(IControllerSession session) : this(session, delegate { return MonotonicClock.NowMs; }) { }
        public CanvasFollower(IControllerSession session, Func<double> now) { this.session = session; this.now = now; }

        public void Begin(int x, int y, decimal velocity)
        { Begin(x, y, velocity, CanvasSettings.Default, 1, DefaultDelayMs, new MotionPoint(0, 0), InterpolationMode.PVT); }

        public void Begin(int x, int y, decimal velocity, CanvasSettings settings, int cs, double followDelayMs, MotionPoint initial)
        { Begin(x, y, velocity, settings, cs, followDelayMs, initial, InterpolationMode.PVT); }

        public void BeginAuto(int x, int y, decimal maximumXCountsPerMs, decimal maximumYCountsPerMs,
            CanvasSettings settings, int cs, double followDelayMs, MotionPoint initial, InterpolationMode interpolation)
        { BeginAuto(x, y, maximumXCountsPerMs, maximumYCountsPerMs, 12M, 12M, settings, cs, followDelayMs, initial, interpolation); }

        public void BeginAuto(int x, int y, decimal maximumXCountsPerMs, decimal maximumYCountsPerMs,
            decimal maximumXCountsPerMsSquared, decimal maximumYCountsPerMsSquared,
            CanvasSettings settings, int cs, double followDelayMs, MotionPoint initial, InterpolationMode interpolation)
        {
            if (maximumXCountsPerMs <= 0 || maximumXCountsPerMs > AxisConfiguration.MaximumSupportedSpeedCtPerMs ||
                maximumYCountsPerMs <= 0 || maximumYCountsPerMs > AxisConfiguration.MaximumSupportedSpeedCtPerMs)
                throw new ArgumentOutOfRangeException("maximumXCountsPerMs");
            if (maximumXCountsPerMsSquared <= 0 || maximumYCountsPerMsSquared <= 0)
                throw new ArgumentOutOfRangeException("maximumXCountsPerMsSquared");
            Begin(x, y, maximumXCountsPerMs, settings, cs, followDelayMs, initial, interpolation);
            ySpeed = maximumYCountsPerMs; automaticSpeed = true;
            maximumXAcceleration = (double)maximumXCountsPerMsSquared * 1000000 / mapping.XCountsPerUnit;
            maximumYAcceleration = (double)maximumYCountsPerMsSquared * 1000000 / mapping.YCountsPerUnit;
        }

        public void Begin(int x, int y, decimal velocity, CanvasSettings settings, int cs, double followDelayMs, MotionPoint initial, InterpolationMode interpolation)
        {
            if (x == y || x < 1 || x > 8 || y < 1 || y > 8) throw new ArgumentException("X/Y 必须选择不同的有效电机。");
            if (velocity <= 0 || velocity > AxisConfiguration.MaximumSupportedSpeedCtPerMs) throw new ArgumentOutOfRangeException("velocity");
            if (settings == null || cs < 1 || cs > 16 || !PlanningOptions.Finite(followDelayMs) || followDelayMs < 0 || followDelayMs > 2000)
                throw new ArgumentException("鼠标随动坐标或延迟参数无效。");
            Interlocked.Increment(ref generation);
            points.Clear(); source = lookahead = null; prepared = null; sourceCurve = null;
            xMotor = x; yMotor = y; speed = velocity; coordinateSystem = cs; mapping = settings; mode = interpolation;
            ySpeed = velocity; automaticSpeed = false;
            maximumXAcceleration = maximumYAcceleration = 100000;
            lastVelocity = new MotionPoint(0, 0);
            MouseSpeedMmPerSecond = FollowSpeedMmPerSecond = 0; SpeedLimited = false;
            delayMs = followDelayMs; lastPoint = initial; lastInputTime = now(); executionEnd = retryAt = 0;
            pending = configured = streamStarted = hasFallback = hasSubmitted = WaitingForBuffer = false;
            streaming = session is IStreamingTrajectorySession;
            Error = null; Active = true;
        }

        public void SubmitMm(MotionPoint point)
        {
            if (!Active || !Finite(point)) return;
            if (!mapping.Contains(point)) { Fail("鼠标随动目标超出坐标范围，已停止。"); return; }
            if (hasSubmitted && PathGeometry.Distance(lastSubmitted, point) < 0.0001) return;
            if (points.Count >= 10000) { Fail("待发送轨迹过多，已停止；请缩短连续拖画时间。"); return; }
            points.Enqueue(new PendingPoint(point, now())); lastSubmitted = point; hasSubmitted = true;
        }

        // Compatibility for older count-based callers.
        public void Submit(MotionPoint target)
        { if (Active && Finite(target)) { latestFallback = target; hasFallback = true; } }

        public void Pump()
        {
            if (!Active || pending) return;
            double time = now();
            double oldest = source != null ? source.TimeMs : lookahead != null ? lookahead.TimeMs : points.Count > 0 ? points.Peek().TimeMs : time;
            if (time - oldest > MaximumInputAgeMs) { Fail("鼠标轨迹已积压超过 15 秒，已停止；请检查平台速度与通信。"); return; }
            // Bound submitted lookahead without blocking the COM/UI thread.
            if (time < retryAt || executionEnd - time > 300) { WaitingForBuffer = true; return; }
            WaitingForBuffer = false;
            if (prepared == null)
            {
                if (source == null)
                {
                    if (streaming)
                    {
                        if (lookahead != null)
                        {
                            if (lookahead.TimeMs > time - delayMs) return;
                            source = lookahead; lookahead = null;
                        }
                        else
                        {
                            if (points.Count == 0 || points.Peek().TimeMs > time - delayMs) return;
                            source = TakeNext(time - delayMs, lastPoint);
                        }
                        // Freeze the next chord before choosing this chord's terminal
                        // velocity. Do not later simplify away a braking constraint.
                        if (points.Count > 0 && points.Peek().TimeMs <= time) lookahead = TakeNext(time, source.Point);
                    }
                    else
                    {
                        if (!hasFallback) return;
                        source = new PendingPoint(mapping.FromCounts(latestFallback.X, latestFallback.Y), time);
                    }
                    double inputDuration = Math.Max(20, source.TimeMs - lastInputTime);
                    double distance = PathGeometry.Distance(source.Point, lastPoint);
                    MouseSpeedMmPerSecond = distance * 1000 / inputDuration;
                    double requestedTime = RequestedTime(inputDuration);
                    if (mode == InterpolationMode.Spline)
                    {
                        double interval = SampleMs / 1000;
                        double xSpeed = (double)speed * 1000 / mapping.XCountsPerUnit, ySpeedMm = (double)ySpeed * 1000 / mapping.YCountsPerUnit;
                        double xStep = Math.Min(xSpeed * interval, 0.5 * maximumXAcceleration * interval * interval);
                        double yStep = Math.Min(ySpeedMm * interval, 0.5 * maximumYAcceleration * interval * interval);
                        double step = Math.Max(0.000001, Math.Min(xStep, yStep));
                        double splineDuration = Math.Max(requestedTime, 1500 * Math.Max(Math.Abs(source.Point.X - lastPoint.X) / Math.Max(0.000001, xSpeed), Math.Abs(source.Point.Y - lastPoint.Y) / Math.Max(0.000001, ySpeedMm)));
                        splineDuration = Math.Max(splineDuration, 1000 * Math.Sqrt(6 * distance / Math.Max(0.000001, Math.Min(maximumXAcceleration, maximumYAcceleration))));
                        sourceSteps = stepsRemaining = Math.Max(1, (int)Math.Ceiling(Math.Max(distance / step, splineDuration / SampleMs)));
                        sourceDuration = SampleMs; sourceCurve = null;
                        sourceStep = new MotionPoint((source.Point.X - lastPoint.X) / sourceSteps, (source.Point.Y - lastPoint.Y) / sourceSteps);
                        FollowSpeedMmPerSecond = distance * 1000 / (sourceSteps * sourceDuration);
                        SpeedLimited = automaticSpeed && sourceSteps * sourceDuration > inputDuration + SampleMs;
                    }
                    else
                    {
                    double safeTime = SafeTime(lastPoint, source.Point, requestedTime);
                    MotionPoint endVelocity = new MotionPoint(0, 0);
                    if (distance > 1e-9 && lookahead != null)
                    {
                        double nextDistance = PathGeometry.Distance(source.Point, lookahead.Point);
                        double dx = source.Point.X - lastPoint.X, dy = source.Point.Y - lastPoint.Y;
                        double nx = lookahead.Point.X - source.Point.X, ny = lookahead.Point.Y - source.Point.Y;
                        // Exact chord following cannot carry a nonzero velocity
                        // through a direction discontinuity. Collinear input can.
                        if (nextDistance > 1e-9 && dx * nx + dy * ny > 0 && Math.Abs(dx * ny - dy * nx) <= 1e-9 * distance * nextDistance)
                        {
                            double nextTime = SafeTime(source.Point, lookahead.Point, RequestedTime(Math.Max(20, lookahead.TimeMs - source.TimeMs)));
                            double terminal = Math.Min(distance * 1000 / safeTime, nextDistance * 1000 / nextTime);
                            endVelocity = new MotionPoint(terminal * dx / distance, terminal * dy / distance);
                        }
                    }
                    sourceCurve = new MotionSegment { Start = lastPoint, End = source.Point, StartVelocity = lastVelocity, EndVelocity = endVelocity, DurationMs = safeTime };
                    while (!WithinLimits(sourceCurve) && safeTime < MaximumInputAgeMs)
                    {
                        safeTime = Math.Min(MaximumInputAgeMs, safeTime * 2);
                        sourceCurve.DurationMs = safeTime;
                    }
                    // The conservative upper time gives nonnegative Bezier velocity
                    // controls and bounds acceleration. Shorten only after analytic
                    // checks of the entire cubic, keeping the starting velocity fixed.
                    double low = requestedTime, high = safeTime;
                    for (int i = 0; i < 32; i++)
                    {
                        double middle = (low + high) / 2;
                        sourceCurve.DurationMs = middle;
                        if (WithinLimits(sourceCurve)) high = middle; else low = middle;
                    }
                    double duration = mode == InterpolationMode.Spline ? Math.Ceiling(high / SampleMs) * SampleMs : high;
                    sourceCurve.DurationMs = duration;
                    if (!WithinLimits(sourceCurve)) { Fail("无法在配置的速度和加速度内生成连续随动轨迹，已停止。"); return; }
                    SpeedLimited = automaticSpeed && duration > inputDuration + (mode == InterpolationMode.Spline ? SampleMs : 0.001);
                    if (duration > MaximumInputAgeMs) { Fail("目标距离与随动约束不匹配，预计超过 15 秒，已停止。"); return; }
                    sourceSteps = stepsRemaining = Math.Max(1, (int)Math.Ceiling(duration / (mode == InterpolationMode.Spline ? SampleMs : 120)));
                    sourceDuration = duration / sourceSteps;
                    FollowSpeedMmPerSecond = distance * 1000 / duration;
                    }
                }
                double fraction = (sourceSteps - stepsRemaining + 1.0) / sourceSteps;
                MotionPoint end = sourceCurve == null ? (stepsRemaining == 1 ? source.Point : new MotionPoint(lastPoint.X + sourceStep.X, lastPoint.Y + sourceStep.Y)) : sourceCurve.At(fraction);
                prepared = new Segment { End = end, Duration = sourceDuration,
                    Velocity = sourceCurve == null ? new MotionPoint(0, 0) : sourceCurve.VelocityAt(fraction) };
            }
            int epoch = generation, x = xMotor, y = yMotor, cs = coordinateSystem;
            InterpolationMode interpolation = mode;
            CanvasSettings currentMapping = mapping; decimal velocity = speed, yVelocity = ySpeed; Segment segment = prepared;
            pending = true;
            session.Execute(delegate
            {
                if (Thread.VolatileRead(ref generation) != epoch) return;
                if (streaming)
                {
                    IStreamingTrajectorySession stream = (IStreamingTrajectorySession)session;
                    if (!streamStarted)
                    {
                        if (interpolation == InterpolationMode.Spline) stream.BeginSplineFollow(cs, x, y, currentMapping);
                        else stream.BeginPvtFollow(cs, x, y, currentMapping);
                        streamStarted = true;
                    }
                    if (Thread.VolatileRead(ref generation) != epoch) return;
                    if (interpolation == InterpolationMode.Spline) stream.AppendSplineFollow(segment.Duration, new MotionPoint[] { segment.End });
                    else stream.AppendPvtFollow(segment.Duration, segment.End, segment.Velocity);
                }
                else
                {
                    if (!configured) { session.SetSpeed(x, velocity); session.SetSpeed(y, yVelocity); }
                    if (Thread.VolatileRead(ref generation) == epoch) session.MovePair(x, y, currentMapping.ToCounts(segment.End));
                }
            }, delegate(Exception failure)
            {
                if (generation != epoch) return;
                pending = false;
                if (failure is FollowBufferBusyException)
                {
                    // No line was written: retain this exact segment for retry.
                    WaitingForBuffer = true; retryAt = now() + 50; return;
                }
                if (failure != null) { Fail(failure.Message); return; }
                configured = true; hasFallback = false; lastPoint = segment.End; lastVelocity = segment.Velocity;
                executionEnd = Math.Max(executionEnd, now()) + segment.Duration;
                prepared = null; stepsRemaining--;
                if (stepsRemaining == 0) { lastInputTime = source.TimeMs; source = null; }
            });
        }

        private double RequestedTime(double inputDuration)
        { return automaticSpeed ? inputDuration : Math.Max(SampleMs, Math.Min(120, inputDuration)); }

        private double SafeTime(MotionPoint start, MotionPoint end, double requested)
        {
            double dx = Math.Abs(end.X - start.X), dy = Math.Abs(end.Y - start.Y);
            // A cubic that starts and ends at zero reaches 1.5 times its
            // average speed. Reserve that factor before the analytic check.
            double time = Math.Max(requested, 1500 * Math.Max(dx / ((double)speed * 1000 / mapping.XCountsPerUnit), dy / ((double)ySpeed * 1000 / mapping.YCountsPerUnit)));
            time = Math.Max(time, 1000 * Math.Sqrt(6 * Math.Max(dx / maximumXAcceleration, dy / maximumYAcceleration)));
            return mode == InterpolationMode.Spline ? Math.Ceiling(time / SampleMs) * SampleMs : time;
        }

        private bool WithinLimits(MotionSegment s)
        {
            double t = s.DurationMs / 1000;
            return TrajectoryMath.PeakVelocity(s.Start.X, s.End.X, s.StartVelocity.X, s.EndVelocity.X, t) <= (double)speed * 1000 / mapping.XCountsPerUnit * (1 + 1e-10) &&
                TrajectoryMath.PeakVelocity(s.Start.Y, s.End.Y, s.StartVelocity.Y, s.EndVelocity.Y, t) <= (double)ySpeed * 1000 / mapping.YCountsPerUnit * (1 + 1e-10) &&
                TrajectoryMath.PeakAcceleration(s.Start.X, s.End.X, s.StartVelocity.X, s.EndVelocity.X, t) <= maximumXAcceleration * (1 + 1e-10) &&
                TrajectoryMath.PeakAcceleration(s.Start.Y, s.End.Y, s.StartVelocity.Y, s.EndVelocity.Y, t) <= maximumYAcceleration * (1 + 1e-10);
        }

        private PendingPoint TakeNext(double releaseBefore, MotionPoint start)
        {
            PendingPoint next = points.Dequeue();
            double startTime = next.TimeMs;
            List<MotionPoint> folded = new List<MotionPoint>(); folded.Add(next.Point);
            while (points.Count > 0 && points.Peek().TimeMs <= releaseBefore && points.Peek().TimeMs - startTime < SampleMs)
            {
                MotionPoint end = points.Peek().Point;
                bool straight = true;
                foreach (MotionPoint point in folded)
                    if (!OnChord(point, start, end)) { straight = false; break; }
                if (!straight) break;
                next = points.Dequeue(); folded.Add(next.Point);
            }
            return next;
        }

        private static bool OnChord(MotionPoint point, MotionPoint start, MotionPoint end)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y, length2 = dx * dx + dy * dy;
            if (length2 < 1e-12) return PathGeometry.Distance(point, start) < 0.0001;
            double u = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / length2;
            if (u < 0 || u > 1) return false; // Retain reversals and overshoots.
            return PathGeometry.Distance(point, new MotionPoint(start.X + u * dx, start.Y + u * dy)) <= ContourToleranceMm;
        }

        private void Fail(string message)
        { Error = message; Cancel(); if (Failed != null) Failed(this, EventArgs.Empty); }

        public void Cancel()
        {
            bool stop = Active; Active = WaitingForBuffer = false;
            points.Clear(); source = lookahead = null; prepared = null; sourceCurve = null; hasFallback = false; Interlocked.Increment(ref generation);
            if (!stop) return;
            int x = xMotor, y = yMotor, cs = coordinateSystem; streamStarted = false;
            InterpolationMode interpolation = mode;
            session.Execute(delegate
            {
                if (!session.IsConnected) return;
                if (session is IStreamingTrajectorySession)
                {
                    IStreamingTrajectorySession stream = (IStreamingTrajectorySession)session;
                    if (interpolation == InterpolationMode.Spline) stream.AbortSplineFollow(cs, x, y); else stream.AbortPvtFollow(cs, x, y);
                }
                else session.Stop(new int[] { x, y });
            }, delegate(Exception failure)
            {
                if (failure != null) { Error = "绘图停止失败：" + failure.Message; if (Failed != null) Failed(this, EventArgs.Empty); }
            });
        }

        private static bool Finite(MotionPoint point) { return PlanningOptions.Finite(point.X) && PlanningOptions.Finite(point.Y); }
    }
}
