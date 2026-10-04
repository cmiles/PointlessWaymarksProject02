using System;
using System.Collections.Generic;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public static class TrackStatisticsCalculator
    {
        private const double EarthRadiusMeters = 6371000.0;
        private const double MinElevationDeltaThreshold = 1.0; // 1 meter threshold to filter elevation jitter

        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusMeters * c;
        }

        public static void RecalculateTrack(IList<TrackPoint> points, TrackStatistics stats)
        {
            if (points == null || points.Count == 0)
            {
                stats.TotalDistanceMeters = 0;
                stats.TotalElevationGainMeters = 0;
                stats.TotalElevationLossMeters = 0;
                stats.MinElevationMeters = 0;
                stats.MaxElevationMeters = 0;
                stats.TotalDuration = TimeSpan.Zero;
                stats.MovingDuration = TimeSpan.Zero;
                stats.AverageSpeedKmh = 0;
                stats.MaxSpeedKmh = 0;
                stats.PointCount = 0;
                ActivityLogService.Instance.Info("Track cleared. Reset statistics to zero.", "Calculations");
                return;
            }

            ActivityLogService.Instance.Info($"Recalculating statistics across {points.Count} points...", "Calculations");

            double totalDistance = 0;
            double movingDistance = 0;
            double elevationGain = 0;
            double elevationLoss = 0;
            double minEle = double.MaxValue;
            double maxEle = double.MinValue;
            TimeSpan movingTime = TimeSpan.Zero;
            double maxSpeedMs = 0;

            TrackPoint prev = null;
            double lastEle = double.NaN;

            // Minimum moving speed threshold: 0.2 m/s (~0.72 km/h or ~0.45 mph)
            // Rejects stationary GPS coordinate jitter while preserving slow uphill hiking and walking.
            const double MovingThresholdMs = 0.2;
            const double MaxPlausibleSpeedMs = 50.0; // ~180 km/h or ~112 mph (filters GPS teleportation spikes)

            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                pt.Index = i;

                if (prev != null)
                {
                    double distDelta = CalculateDistanceMeters(prev.Latitude, prev.Longitude, pt.Latitude, pt.Longitude);

                    bool hasTime = prev.Time.HasValue && pt.Time.HasValue && pt.Time.Value >= prev.Time.Value;
                    double dtSec = hasTime ? (pt.Time.Value - prev.Time.Value).TotalSeconds : 0;

                    // Determine segment speed
                    double speedMs;
                    if (pt.Speed.HasValue && pt.Speed.Value >= 0)
                    {
                        speedMs = pt.Speed.Value;
                    }
                    else if (dtSec > 0)
                    {
                        speedMs = distDelta / dtSec;
                        pt.Speed = Math.Round(speedMs, 2);
                    }
                    else
                    {
                        speedMs = 0;
                    }

                    // Classify segment as moving vs stationary stopped GPS drift
                    bool isMoving;
                    if (hasTime && dtSec > 0)
                    {
                        isMoving = speedMs >= MovingThresholdMs && speedMs <= MaxPlausibleSpeedMs;
                    }
                    else
                    {
                        isMoving = distDelta > 0.05;
                    }

                    if (isMoving)
                    {
                        totalDistance += distDelta;
                        movingDistance += distDelta;

                        if (hasTime && dtSec > 0)
                        {
                            movingTime += TimeSpan.FromSeconds(dtSec);
                            if (speedMs <= MaxPlausibleSpeedMs && speedMs > maxSpeedMs)
                            {
                                maxSpeedMs = speedMs;
                            }
                        }
                    }
                    else
                    {
                        // Stopped / stationary pause:
                        // Only add distDelta if it represents significant physical displacement over a long pause,
                        // rather than 0-3 meter stationary GPS coordinate drift.
                        if (distDelta >= 10.0 && dtSec < 1800)
                        {
                            totalDistance += distDelta;
                        }
                    }
                }

                pt.DistanceFromStart = totalDistance;

                if (pt.Elevation.HasValue)
                {
                    double ele = pt.Elevation.Value;
                    if (ele < minEle) minEle = ele;
                    if (ele > maxEle) maxEle = ele;

                    if (!double.IsNaN(lastEle))
                    {
                        double eleDelta = ele - lastEle;
                        if (Math.Abs(eleDelta) >= MinElevationDeltaThreshold)
                        {
                            if (eleDelta > 0) elevationGain += eleDelta;
                            else elevationLoss += Math.Abs(eleDelta);
                            lastEle = ele;
                        }
                    }
                    else
                    {
                        lastEle = ele;
                    }
                }

                prev = pt;
            }

            if (minEle == double.MaxValue) minEle = 0;
            if (maxEle == double.MinValue) maxEle = 0;

            TimeSpan totalDuration = TimeSpan.Zero;
            if (points.Count > 1 && points[0].Time.HasValue && points[points.Count - 1].Time.HasValue)
            {
                totalDuration = points[points.Count - 1].Time.Value - points[0].Time.Value;
                if (totalDuration < TimeSpan.Zero) totalDuration = TimeSpan.Zero;
            }

            if (movingTime == TimeSpan.Zero && totalDuration > TimeSpan.Zero)
            {
                movingTime = totalDuration;
                movingDistance = totalDistance;
            }

            // Calculate average speed without the totalDistance / movingTime inflation bug.
            // Moving speed = movingDistance / movingTime.
            // If movingTime is unavailable, falls back to totalDistance / totalDuration.
            double avgSpeedKmh = 0;
            if (movingTime.TotalHours > 0 && movingDistance > 0)
            {
                avgSpeedKmh = (movingDistance / 1000.0) / movingTime.TotalHours;
            }
            else if (totalDuration.TotalHours > 0 && totalDistance > 0)
            {
                avgSpeedKmh = (totalDistance / 1000.0) / totalDuration.TotalHours;
            }

            stats.TotalDistanceMeters = totalDistance;
            stats.TotalElevationGainMeters = elevationGain;
            stats.TotalElevationLossMeters = elevationLoss;
            stats.MinElevationMeters = minEle;
            stats.MaxElevationMeters = maxEle;
            stats.TotalDuration = totalDuration;
            stats.MovingDuration = movingTime;
            stats.AverageSpeedKmh = avgSpeedKmh;
            stats.MaxSpeedKmh = maxSpeedMs * 3.6;
            stats.PointCount = points.Count;

            ActivityLogService.Instance.Success(
                $"Stats updated: {totalDistance / 1609.344:F2} mi | Ascent: +{elevationGain * 3.280839895:F0} ft | Ele: [{minEle * 3.280839895:F0} ft .. {maxEle * 3.280839895:F0} ft] | Avg Speed: {avgSpeedKmh / 1.609344:F1} mph",
                "Calculations");
        }

        private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
    }
}
