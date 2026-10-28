using System;

namespace Operations_Control.Motion
{
    public sealed class ManualMotionProfile
    {
        public double MaximumAcceleration { get; private set; }
        public int AccelerationTimeMs { get; private set; }
        public int SCurveTimeMs { get; private set; }

        internal ManualMotionProfile(double acceleration, int accelerationTimeMs, int sCurveTimeMs)
        {
            MaximumAcceleration = acceleration;
            AccelerationTimeMs = accelerationTimeMs;
            SCurveTimeMs = sCurveTimeMs;
        }
    }

    public static class PointToPointProfile
    {
        private const double SCurveFraction = 0.25;

        public static ManualMotionProfile Calculate(double speed, double acceleration)
        {
            if (Double.IsNaN(speed) || Double.IsInfinity(speed) || speed <= 0 || speed > 15)
                throw new ArgumentOutOfRangeException("speed", "定位速度应大于 0 且不超过 15 ct/ms。");
            if (Double.IsNaN(acceleration) || Double.IsInfinity(acceleration) || acceleration <= 0 || acceleration > 12)
                throw new ArgumentOutOfRangeException("acceleration", "定位加速度应大于 0 且不超过 12 ct/ms^2。");

            double total = Math.Ceiling(speed / (acceleration * (1 - SCurveFraction)));
            if (total < 4) total = 4;
            if (total > 1000000) throw new ArgumentOutOfRangeException("acceleration", "加速时间超过允许范围。");

            int accelerationTime = (int)total;
            int sCurveTime = (int)Math.Floor(accelerationTime * SCurveFraction);
            if (sCurveTime < 1) sCurveTime = 1;
            if (2 * sCurveTime >= accelerationTime) sCurveTime = (accelerationTime - 1) / 2;
            return new ManualMotionProfile(acceleration, accelerationTime, sCurveTime);
        }

        public static double EstimateDuration(double distance, double speed, ManualMotionProfile profile)
        {
            if (Double.IsNaN(distance) || Double.IsInfinity(distance) || distance < 0 || profile == null)
                throw new ArgumentException("点到点距离或参数无效。");
            if (distance == 0) return 0;
            double accelerationDistance = speed * profile.AccelerationTimeMs / 2;
            double cruise = Math.Max(0, (distance - 2 * accelerationDistance) / speed);
            return 2 * profile.AccelerationTimeMs + cruise;
        }
    }

    public sealed class SCurveTrajectory
    {
        private readonly double start, direction, distance, peakSpeed;
        private readonly double accelerationTime, sCurveTime, cruiseTime, peakAcceleration;
        public double Target { get; private set; }
        public double DurationMs { get; private set; }

        public SCurveTrajectory(double start, double target, double maximumSpeed, double maximumAcceleration,
            double accelerationTimeMs, double sCurveTimeMs)
        {
            if (!Finite(start) || !Finite(target) || !Finite(maximumSpeed) || maximumSpeed <= 0 ||
                !Finite(maximumAcceleration) || maximumAcceleration <= 0 ||
                !Finite(accelerationTimeMs) || accelerationTimeMs < 4 || !Finite(sCurveTimeMs) ||
                sCurveTimeMs <= 0 || 2 * sCurveTimeMs >= accelerationTimeMs)
                throw new ArgumentException("点到点 S 曲线参数无效。");

            this.start = start;
            Target = target;
            direction = Math.Sign(target - start);
            distance = Math.Abs(target - start);
            if (distance == 0) return;

            accelerationTime = Math.Max(accelerationTimeMs, Math.Ceiling(maximumSpeed / maximumAcceleration + sCurveTimeMs));
            sCurveTime = Math.Min(sCurveTimeMs, Math.Floor((accelerationTime - 1) / 2));
            peakSpeed = Math.Min(maximumSpeed, distance / accelerationTime);
            peakAcceleration = peakSpeed / (accelerationTime - sCurveTime);
            double accelerationDistance = peakSpeed * accelerationTime / 2;
            cruiseTime = Math.Max(0, (distance - 2 * accelerationDistance) / peakSpeed);
            DurationMs = 2 * accelerationTime + cruiseTime;
        }

        public double PositionAt(double elapsedMs)
        {
            if (distance == 0 || elapsedMs >= DurationMs) return Target;
            if (elapsedMs <= 0) return start;

            double accelerationDistance = peakSpeed * accelerationTime / 2;
            double position;
            if (elapsedMs < accelerationTime)
                position = AccelerationPosition(elapsedMs);
            else if (elapsedMs < accelerationTime + cruiseTime)
                position = accelerationDistance + peakSpeed * (elapsedMs - accelerationTime);
            else
            {
                double decelerationElapsed = elapsedMs - accelerationTime - cruiseTime;
                position = distance - AccelerationPosition(accelerationTime - decelerationElapsed);
            }
            return start + direction * Math.Max(0, Math.Min(distance, position));
        }

        private double AccelerationPosition(double elapsedMs)
        {
            double t = Math.Max(0, Math.Min(accelerationTime, elapsedMs));
            double jerk = peakAcceleration / sCurveTime;
            if (t <= sCurveTime) return jerk * t * t * t / 6;

            double firstDistance = peakAcceleration * sCurveTime * sCurveTime / 6;
            double firstVelocity = peakAcceleration * sCurveTime / 2;
            double linearEnd = accelerationTime - sCurveTime;
            if (t <= linearEnd)
            {
                double linearElapsed = t - sCurveTime;
                return firstDistance + firstVelocity * linearElapsed + peakAcceleration * linearElapsed * linearElapsed / 2;
            }

            double linearDuration = linearEnd - sCurveTime;
            double linearDistance = firstDistance + firstVelocity * linearDuration + peakAcceleration * linearDuration * linearDuration / 2;
            double linearVelocity = firstVelocity + peakAcceleration * linearDuration;
            double finalElapsed = t - linearEnd;
            return linearDistance + linearVelocity * finalElapsed + peakAcceleration * finalElapsed * finalElapsed / 2 -
                jerk * finalElapsed * finalElapsed * finalElapsed / 6;
        }

        private static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    }
}
