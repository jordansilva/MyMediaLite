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
using System.Linq;
using System.Collections.Generic;
using MyMediaLite.ItemRecommendation;
using MyMediaLite.Data;
using MyMediaLite.IO;
using System.Reflection;

namespace Baselines.Algorithms
{
	public class RunBPRMF : IBaseline
	{
		private const string ALGORITHM_NAME = "BPRMF";
		public BPRMF Algorithm;

		public RunBPRMF ()
		{
			// num_factors=10 bias_reg=0 reg_u=0.0025 reg_i=0.0025 reg_j=0.00025 num_iter=30 learn_rate=0.05 uniform_user_sampling=True with_replacement=False update_j=True
			Algorithm = new BPRMF ();
			Algorithm.BiasReg = 0;
			Algorithm.NumFactors = 10;
			Algorithm.RegU = 0.0025f;
			Algorithm.RegI = 0.0025f;
			Algorithm.RegJ = 0.00025f;
			Algorithm.NumIter = 30;
			Algorithm.LearnRate = 0.05f;
			Algorithm.UniformUserSampling = true;
			Algorithm.WithReplacement = false;
			Algorithm.UpdateJ = true;

			//new IdentityMapping()
			//Algorithm.
		}

		public RunBPRMF (string model)
		{
			Algorithm = new BPRMF ();
			Algorithm.LoadModel (model);
		}

		public void LoadModel (string filename)
		{
			Algorithm = new BPRMF ();
			Algorithm.LoadModel (filename);
		}

		public void SetParameter (string name, object value)
		{
			try {
				PropertyInfo property = Algorithm.GetType ().GetProperty (name);
				if (property != null) {
					if (value is int)
						value = Convert.ToUInt32(value);
					property.SetValue (Algorithm, value);
				}
			} catch (Exception ex) {
				Console.WriteLine ("Parameter is not configurable: {0}, {1} - Error: {2}", name, value, ex.Message);
			}
		}

		public IList<Tuple<int, float>> Predict (int user, IList<int> items)
		{
			var predictions = new List<Tuple<int, float>> ();

			foreach (int item in items) {
				Tuple<int, float> rating = Predict (user, item);
				predictions.Add (rating);
			}

			predictions = predictions.OrderByDescending (x => x.Item2).ToList ();
			return predictions;
		}

		public Tuple<int, float> Predict (int user, int item)
		{
			float rating = Algorithm.Predict (user, item);
			return Tuple.Create (item, rating);
		}

		public void Train (string training, ItemDataFileFormat file_format)
		{
			IPosOnlyFeedback feedback = ItemData.Read (training,
													   new IdentityMapping (),
													   new IdentityMapping (),
													   file_format == ItemDataFileFormat.IGNORE_FIRST_LINE);

			Algorithm.Feedback = feedback;
			Algorithm.Train ();
		}

		public void TunningParameters ()
		{
			
		}

		public override string ToString ()
		{
			return Algorithm.ToString ();
		}

		public string Name ()
		{
			return ALGORITHM_NAME;
		}
	}
}
