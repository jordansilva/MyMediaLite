// Copyright (C) 2011, 2012 Zeno Gantner
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

using System;
using NUnit.Framework;
using MyMediaLite.RatingPrediction;
using MyMediaLite.Helper;

namespace Tests.RatingPrediction
{
	[TestFixture()]
	public class RankGeoFMTest
	{
		const string FILENAME_ITEMS = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/places.txt";

		[Test()]
		public void TestNearestNeighborsItem()
		{
			var recommender = new RankGeoFM();
			recommender.Items = Utils.ReadPOIs (FILENAME_ITEMS);

			recommender.Train ();
			var items = recommender.GetNearestNeighborsItem (1);

			Assert.AreEqual( recommender.K, items.Count );
		}

	}
}