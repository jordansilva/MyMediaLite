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
using MyMediaLite.ItemRecommendation;
using MyMediaLite.Data;

namespace Baselines.Algorithms
{
	public class RunBPRMF
	{
		protected BPRMF mAlgorithm;

		public RunBPRMF()
		{
			// num_factors=10 bias_reg=0 reg_u=0.0025 reg_i=0.0025 reg_j=0.00025 num_iter=30 learn_rate=0.05 uniform_user_sampling=True with_replacement=False update_j=True
			mAlgorithm = new BPRMF ();
			mAlgorithm.BiasReg = 0;
			mAlgorithm.NumFactors = 10;
			mAlgorithm.RegU = 0.0025f;
			mAlgorithm.RegI = 0.0025f;
			mAlgorithm.RegJ = 0.00025f;
			mAlgorithm.NumIter = 30;
			mAlgorithm.LearnRate = 0.05f;
			mAlgorithm.UniformUserSampling = true;
			mAlgorithm.WithReplacement = false;
			mAlgorithm.UpdateJ = true;
		}

		public RunBPRMF (string model) : this()
		{
			mAlgorithm.LoadModel(model);
		}

		public IList<Tuple<int, float>> Predict (int user, IList<int> items)
		{
			List<Tuple<int, float>> predictions = new List<Tuple<int, float>> ();

			foreach (int item in items) {
				Tuple<int, float> rating = Predict(user, item);
				predictions.Add (rating);
			}

			return predictions;
		}

		public Tuple<int, float> Predict (int user, int item)
		{
			float rating = mAlgorithm.Predict (user, item);
			return Tuple.Create(item, rating);
		}

		public void Train ()
		{
			mAlgorithm.Train();
		}
	}
}
