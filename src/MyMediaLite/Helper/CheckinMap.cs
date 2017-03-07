﻿// Copyright (C) 2015 Zeno Gantner
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
using CsvHelper.Configuration;
using MyMediaLite.Data;

namespace MyMediaLite.Helper
{
	///
	public sealed class CheckinMap : CsvClassMap<Checkin>
	{
		///
		public CheckinMap ()
		{
			Map (m => m.User).Name ("user");
			Map (m => m.Item).Name ("venue");
			Map (m => m.Coordinates).Name ("point").TypeConverter(new CoordinatorConverter());
			Map (m => m.Date).Name ("time");
			Map (m => m.Candidates).Name ("candidates").TypeConverter (new EnumarableConverter ());
		}
	}
}
