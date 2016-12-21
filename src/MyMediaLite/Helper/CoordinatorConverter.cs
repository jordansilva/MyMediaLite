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
using CsvHelper.Configuration;
using MyMediaLite.Data;

namespace MyMediaLite.Helper
{
	public class CoordinatorConverter : CsvHelper.TypeConversion.EnumerableConverter
	{
		public override bool CanConvertFrom (Type type)
		{
			return type == typeof (String) || type == typeof (string);
		}

		public override object ConvertFromString (CsvHelper.TypeConversion.TypeConverterOptions options, string text)
		{
			try {
				string [] arrText = text.Replace ("[", "").Replace ("]", "").Split (',');
				double longitude = double.Parse (arrText [0]);
				double latitude = double.Parse (arrText [1]);
				return new Coordinate (latitude, longitude);
			} catch (Exception ex) {
				Console.WriteLine (text);
				throw ex;
			}
		}
	}
}
