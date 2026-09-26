using System;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Astronomical sun position (NOAA solar-position equations). Given a location, day of the year
    /// and local clock time, returns the sun's elevation above the horizon and azimuth from north, so
    /// the sun can be placed realistically for any city / date / time.
    /// </summary>
    public static class SolarPosition
    {
        const double Deg2Rad = Math.PI / 180.0;
        const double Rad2Deg = 180.0 / Math.PI;

        /// <param name="latDeg">Latitude, degrees (N positive).</param>
        /// <param name="lonDeg">Longitude, degrees (E positive).</param>
        /// <param name="utcOffsetHours">Local standard time offset from UTC, hours.</param>
        /// <param name="dayOfYear">1..365/366.</param>
        /// <param name="localHour">Local clock time, hours (0..24).</param>
        /// <param name="elevationDeg">Out: sun elevation above the horizon (negative = below).</param>
        /// <param name="azimuthDeg">Out: sun azimuth, degrees clockwise from north (0=N, 90=E, 180=S, 270=W).</param>
        public static void Compute(
            double latDeg, double lonDeg, double utcOffsetHours, int dayOfYear, double localHour,
            out double elevationDeg, out double azimuthDeg)
        {
            double lat = latDeg * Deg2Rad;

            // Fractional year (radians).
            double gamma = 2.0 * Math.PI / 365.0 * (dayOfYear - 1 + (localHour - 12.0) / 24.0);

            // Equation of time (minutes) and solar declination (radians) — Fourier fits.
            double eqTime = 229.18 * (0.000075
                + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma)
                - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));
            double decl = 0.006918
                - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma)
                - 0.006758 * Math.Cos(2 * gamma) + 0.000907 * Math.Sin(2 * gamma)
                - 0.002697 * Math.Cos(3 * gamma) + 0.001480 * Math.Sin(3 * gamma);

            // True solar time (minutes) → hour angle (radians). Longitude and time zone set when
            // local noon actually is; the equation of time is the analemma correction.
            double timeOffset = eqTime + 4.0 * lonDeg - 60.0 * utcOffsetHours;
            double trueSolarTime = localHour * 60.0 + timeOffset;
            double hourAngle = (trueSolarTime / 4.0 - 180.0) * Deg2Rad; // negative before solar noon

            double sinElev = Math.Sin(lat) * Math.Sin(decl)
                + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(hourAngle);
            sinElev = Math.Max(-1.0, Math.Min(1.0, sinElev));
            elevationDeg = Math.Asin(sinElev) * Rad2Deg;

            // Azimuth from south (positive toward west), then rotate to clockwise-from-north.
            double azFromSouth = Math.Atan2(
                Math.Sin(hourAngle),
                Math.Cos(hourAngle) * Math.Sin(lat) - Math.Tan(decl) * Math.Cos(lat));
            azimuthDeg = ((azFromSouth * Rad2Deg) + 180.0) % 360.0;
            if (azimuthDeg < 0.0)
            {
                azimuthDeg += 360.0;
            }
        }

        /// <summary>
        /// Unit vector pointing TOWARD the sun in Unity world space, with +Z = north, +X = east, +Y = up.
        /// A directional light should aim along the negative of this (it shines from the sun downward).
        /// </summary>
        public static Vector3 DirectionToSun(double elevationDeg, double azimuthDeg)
        {
            double elev = elevationDeg * Deg2Rad;
            double az = azimuthDeg * Deg2Rad;
            double horiz = Math.Cos(elev);
            return new Vector3(
                (float)(horiz * Math.Sin(az)), // east
                (float)Math.Sin(elev),         // up
                (float)(horiz * Math.Cos(az)));// north
        }

        /// <summary>Day-of-year (1..366) for a month/day, using a non-leap table (good enough for sun travel).</summary>
        public static int DayOfYear(int month, int day)
        {
            int[] cum = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
            month = Mathf.Clamp(month, 1, 12);
            day = Mathf.Clamp(day, 1, 31);
            return cum[month - 1] + day;
        }
    }
}
