// Copyright (C) 2012 Zeno Gantner
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
using System.Runtime.Serialization;
using MyMediaLite.Data;

namespace Baselines.Helper
{
	[Serializable()]
	public class FoldTest : ISerializable
	{
		public IList<Checkin> Training { get; set; } 
		public IList<Checkin> Validation { get; set; }
		public IList<Checkin> Test { get; set; }

		public FoldTest ()
		{
		}

		public FoldTest (SerializationInfo info, StreamingContext context) {
			Training = (IList<Checkin>)info.GetValue ("Training", typeof (IList<Checkin>));
			Validation = (IList<Checkin>)info.GetValue ("Validation", typeof (IList<Checkin>));
			Test = (IList<Checkin>)info.GetValue ("Test", typeof (IList<Checkin>));
		}

		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("Training", Training);
			info.AddValue ("Validation", Validation);
			info.AddValue ("Test", Test);
		}
	}
}
