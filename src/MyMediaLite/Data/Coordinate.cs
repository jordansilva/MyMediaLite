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
using System.Runtime.Serialization;

namespace MyMediaLite.Data
{
	[Serializable]
	public class Coordinate : ISerializable
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }

		public Coordinate ()
		{
		}

		public Coordinate (SerializationInfo info, StreamingContext context)
		{
			Latitude = (double)info.GetValue ("Latitude", typeof (double));
			Longitude = (double)info.GetValue ("Longitude", typeof (double));
		}


		public Coordinate (double lat, double lng)
		{
			Latitude = lat;
			Longitude = lng;
		}

		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("Latitude", Latitude);
			info.AddValue ("Longitude", Longitude);
		}

		public override string ToString ()
		{
			return string.Format ("[{0},{1}]", Convert.ToString(Longitude), Convert.ToString(Latitude));
		}
	}
}
