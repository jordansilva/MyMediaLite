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
using System.Collections.Generic;
using CsvHelper.Configuration;

namespace MyMediaLite.Data
{
	public class POI
	{
		public int Id { get; set; }
		public Coordinate Coordinates { get; set; }
		public POI ()
		{
		}

		public POI (int id, float lat, float lng)
		{
			Id = id;
			Coordinates = new Coordinate (lat, lng);
		}
	}

	public sealed class POIMap : CsvClassMap<POI>
	{
		public POIMap ()
		{
			Map (m => m.Id).Name ("uid");
			References<POICoordinateMap> (m => m.Coordinates);
		}
	}

	public sealed class POICoordinateMap : CsvClassMap<Coordinate>
	{
		public POICoordinateMap ()
		{
			Map (m => m.Latitude).Index (0);
			Map (m => m.Longitude).Index (1);
		}
	}
}
