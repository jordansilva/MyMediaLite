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
using Baselines.Algorithms;
using System.Linq;
using MyMediaLite.ItemRecommendation;
using System.Collections.Generic;
using MyMediaLite;
using MyMediaLite.RatingPrediction;
using MyMediaLite.IO;
using MyMediaLite.Data;

namespace Baselines.Commands
{

	public class RankGeoFMCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		ITimedRatings FeedbackRatings;

		public RankGeoFMCommand (string training, string test) : base (training, test, typeof (WeatherContextAwareItemRecommender))
		{

			((WeatherContextAwareItemRecommender)Recommender).weather_aware = false;
			((WeatherContextAwareItemRecommender)Recommender).max_iter = 7000;
			((WeatherContextAwareItemRecommender)Recommender).evaluation_at = 20;

			((WeatherContextAwareItemRecommender)Recommender).beta = 0;


			((BPRMF)Recommender).NumFactors = 10;
			((BPRMF)Recommender).RegU = 0.0025f;
			((BPRMF)Recommender).RegI = 0.0025f;
			((BPRMF)Recommender).RegJ = 0.00025f;
			((BPRMF)Recommender).NumIter = 25;
			((BPRMF)Recommender).LearnRate = 0.05f;
			((BPRMF)Recommender).UniformUserSampling = true;
			((BPRMF)Recommender).WithReplacement = false;
			((BPRMF)Recommender).UpdateJ = true;
		}

		protected override void Init ()
		{
			if (!string.IsNullOrEmpty (path_training)) {
				Console.WriteLine ("Loading training data");
				FeedbackRatings = TimedRatingData.Read (path_training, new IdentityMapping (), new IdentityMapping (),
				                                     TestRatingFileFormat.WITHOUT_RATINGS, true);	
			}

			if (!string.IsNullOrEmpty (path_test)) {
				Console.WriteLine ("Loading test data");
				Test = LoadTest (path_test);
			}
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			//log.Info ("Tunning Regularization parameter");
			//TunningRegularization ();

			//log.Info ("Tunning Latent Factors parameter");
			//TunningLatentFactors ();

			//log.Info ("Tunning Learning Rate parameter");
			//TunningLearningRate ();
		}

		QueryResult Train (uint num_factors, float learn_rate, float regularization)
		{
			bool evaluate = true;
			QueryResult result = null;
			while (evaluate) {
				try {
					CreateModel (typeof (BPRMF));
					((BPRMF)Recommender).Feedback = Feedback;
					((BPRMF)Recommender).NumIter = 25;
					((BPRMF)Recommender).NumFactors = num_factors;
					((BPRMF)Recommender).LearnRate = learn_rate;
					((BPRMF)Recommender).RegI = regularization;
					((BPRMF)Recommender).RegU = regularization;
					((BPRMF)Recommender).RegJ = regularization * 0.1f;

					TimeSpan t = Wrap.MeasureTime (delegate () {
						Train ();
						result = Evaluate ();
					});

					Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
					evaluate = false;
				} catch (Exception ex) {
					Console.WriteLine (ex.Message);
					evaluate = true;
				}
			}

			return result;
		}

		void Log (uint num_factors, float regularization, float learn_rate, double metric)
		{
			log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t-\t\tMRR = {3}", num_factors,
									 learn_rate, regularization, metric));
		}

		public override void Evaluate (string filename)
		{
			throw new NotImplementedException ();
		}
	}
}
