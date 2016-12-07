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
		public float Latitude { get; set; }
		public float Longitude { get; set; }

		public Coordinate ()
		{
		}

		public Coordinate (SerializationInfo info, StreamingContext context)
		{
			Latitude = (float)info.GetValue ("Latitude", typeof (float));
			Longitude = (float)info.GetValue ("Longitude", typeof (float));
		}


		public Coordinate (float lat, float lng)
		{
			Latitude = lat;
			Longitude = lng;
		}

		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("Latitude", Latitude);
			info.AddValue ("Longitude", Longitude);
		}
	}
}
