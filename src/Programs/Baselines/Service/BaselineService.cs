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
using System.IO;
using System.Linq;
using Baselines.Algorithms;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.Eval.Measures;
using MyMediaLite.IO;

namespace Baselines.Service
{
	public class BaselineService
	{
		private string pFileTraining;
		private string pFileTest;

		IList<Checkin> Test;

		public BaselineService (string training, string test)
		{
			pFileTraining = training;
			pFileTest = test;

			LoadFiles ();
		}

		private void LoadFiles ()
		{
			if (Test == null) {
				//Reading test file
				Console.WriteLine ("Loading data: {0}", pFileTest);
				TimeSpan t = Wrap.MeasureTime (delegate () { Test = MyMediaLite.Helper.Utils.ReadCheckins (pFileTest); });
				Console.WriteLine ("Read data: {0} seconds", t.TotalSeconds);
			}
		}

		//Create algorithm
		public IBaseline CreateModel (Type baseline)
		{
			if (baseline != null && baseline.GetInterface (typeof (IBaseline).FullName) != null) {
				return Activator.CreateInstance (baseline) as IBaseline;
			} else
				throw new InvalidCastException ("Baseline must to implement IBaseline interface");
		}

		public void TrainModel (IBaseline baseline, ItemDataFileFormat file_format)
		{
			if (baseline != null) {
				baseline.Train (pFileTraining, file_format);
			} else
				throw new NullReferenceException ("Baseline can not be null");
		}

		//Load algorithm model
		public IBaseline LoadModel (Type baseline, string filename)
		{
			if (baseline != null && baseline.GetInterface (typeof (IBaseline).FullName) != null) {
				var algorithm = Activator.CreateInstance (baseline) as IBaseline;
				TimeSpan t = Wrap.MeasureTime (delegate () { algorithm.LoadModel (filename); });
				Console.WriteLine ("Loading model: {0} seconds", t.TotalSeconds);

				return algorithm;
			} else
				throw new InvalidCastException ("Baseline must to implement IBaseline interface");

		}

		public QueryResult EvaluationRank (IBaseline baseline)
		{
			var queryResult = new QueryResult (baseline.Name (), baseline.ToString ());
			int i = 0;
			double evaluation = 0.0f;
			foreach (Checkin item in Test) {
				i++;
				IList<Tuple<int, float>> ratings = baseline.Predict (item.User, item.Candidates);
				queryResult.Add (i, ratings, string.Format ("u{0}", item.User));


				List<int> itemsId = ratings.Select (x => x.Item1).ToList ();
				int[] rel = { item.Item };
				evaluation += ReciprocalRank.Compute (itemsId, rel);
			}


			evaluation = evaluation / (i * 1.0f);
			queryResult.AddMetric ("MRR", evaluation);
			return queryResult;
		}


	}
}
