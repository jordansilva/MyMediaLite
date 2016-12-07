// Copyright (C) 2015 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
namespace MyMediaLite.Helper
{
	public class DistanceHelper
	{
		/// <summary>
		/// Calculates the distance between two coordinates using the Haversine algorithm.
		/// </summary>
		/// <param name="lat1">First Latitude</param>
		/// <param name="lon1">First Longitude</param>
		/// <param name="lat2">Second Latitude</param>
		/// <param name="lon2">Second Longitude</param>
		/// <returns>Returns a double indicating the distance in kilometers</returns>
		public static double Distance (double lat1, double lon1, double lat2, double lon2)
		{
			double radLat1 = Degrees2Radians (lat1);
			double radLat2 = Degrees2Radians (lat2);
			double radLon1 = Degrees2Radians (lon1);
			double radLon2 = Degrees2Radians (lon2);
			double a = radLat1 - radLat2;
			double b = radLon1 - radLon2;

			double dist = Math.Pow (Math.Sin (a / 2), 2) + Math.Cos (radLat1) * Math.Cos (radLat2) *
							  Math.Pow (Math.Sin (b / 2), 2);
			dist = 2 * Math.Sin (Math.Sqrt (dist));
			dist *= 6371.004;
			return dist;
		}

		/// <summary>
		/// This function converts decimal degrees to radians
		/// </summary>
		/// <param name="deg">Degrees</param>
		public static double Degrees2Radians (double deg)
		{
			return (deg * Math.PI / 180.0);
		}

		/// <summary>
		/// This function converts radians to decimal degrees
		/// </summary>
		/// <param name="rad">Radians</param>
		public static double Radians2Degrees (double rad)
		{
			return (rad / Math.PI * 180.0);
		}

	}
}
